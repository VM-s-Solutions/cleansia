using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.Infra.Services.Pdf;
using Cleansia.Infra.Services.Pdf.Models;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using CampaignProgressRow = Cleansia.Core.Domain.Messaging.CampaignProgress;
using StripeException = Stripe.StripeException;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0064 D2 on Postgres with two operating companies (TC-LC-WD-1..4). Company B announces its
/// last day; the sweep runs through the real container — real repositories, refund service, credit
/// unwind, notification producer and pay-period service — with Stripe, the queue, SendGrid, the PDF
/// renderer and blob storage replaced by recording doubles. Every assertion reads the rows back
/// through a fresh context, and every "converges" claim is a second or third delivery of the same
/// message counted against the doubles.
/// </summary>
[Collection("PostgresCollection")]
public sealed class CompanyWindDownSweepTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string A = TestTenants.Default;
    private const string B = TestTenants.Second;
    private const string AdminBId = "01WD-ADMIN-B-000000000000";
    private const string SvkId = "country-svk-winddown";
    private const string CzeId = "country-cze-winddown";
    private const string EurId = "currency-eur-winddown";
    private const string CzkId = "currency-czk-winddown";
    private const string PlanId = "plan-winddown";
    private static readonly DateOnly WindDownFrom = new(2026, 10, 1);
    private static readonly DateTimeOffset RequestedOn = new(2026, 9, 16, 8, 30, 0, TimeSpan.Zero);
    private static readonly string Code = RequestedOn.UtcDateTime.ToString("yyyyMMddHHmmss");

    // 00:00 on the wind-down date in Bratislava is 22:00 UTC the evening before.
    private static readonly DateTime JustBeforeCutoff = new(2026, 9, 30, 21, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WellBefore = new(2026, 9, 20, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AfterCutoff = new(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LaterAfterCutoff = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

    private readonly RecordingStripeClient _stripe = new();
    private readonly RecordingQueueClient _queue = new();
    private readonly RecordingEmailService _email = new();

    private sealed record Seeded(
        string ConfirmedCustomerId,
        string ApprovedCleanerId,
        string ApprovedCleanerEmployeeId,
        string BeforeCashOrderId,
        string BeforeCardPaidOrderId,
        string AfterCardPaidOrderId,
        string AfterCashOrderId,
        string AfterCardUnpaidOrderId,
        string ACardPaidOrderId,
        string ActiveTemplateId,
        string PausedTemplateId,
        string ATemplateId,
        string EurMembershipId,
        string CzkMembershipId,
        string AMembershipId,
        string CreditAccountId,
        string EmptyCreditAccountId,
        string ACustomerId);

    private Task SetupAsync(IServiceCollection services)
    {
        services.AddScoped<IPayPeriodBackgroundService, PayPeriodBackgroundService>();
        services.AddScoped<ICompanyWindDownService, CompanyWindDownService>();
        services.Replace(ServiceDescriptor.Singleton<IStripeClient>(_stripe));
        services.Replace(ServiceDescriptor.Singleton<IStripeClientFactory>(new StubStripeClientFactory(_stripe)));
        services.Replace(ServiceDescriptor.Singleton<IQueueClient>(_queue));
        services.Replace(ServiceDescriptor.Singleton<IEmailService>(_email));
        services.Replace(ServiceDescriptor.Singleton<IPdfService>(StubPdfService()));
        services.Replace(ServiceDescriptor.Singleton<IBlobContainerClientFactory>(StubBlobFactory()));
        return Task.CompletedTask;
    }

    private static async Task<CompanyWindDownRunSummary> RunSweepAsync(IServiceProvider provider, string tenantId)
    {
        // Each delivery is its own scope, as it is under the Functions host.
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICompanyWindDownService>().RunAsync(tenantId, CancellationToken.None);
    }

    [Fact]
    public async Task The_sweep_tells_everyone_cancels_and_refunds_after_the_date_pauses_templates_and_ends_every_Plus_then_converges_on_a_second_run()
    {
        Seeded seeded = default!;
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                seeded = await SeedTwoCompaniesAsync(ctx, deactivateB: false);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var first = await RunSweepAsync(provider, B);
                var lastRunAfterFirst = await LastRunOnAsync(provider);
                var second = await RunSweepAsync(provider, B);
                return (First: first, Second: second, LastRunAfterFirst: lastRunAfterFirst);
            },
            assert: async (ctx, runs) =>
            {
                Assert.True(runs.First.Ran);
                Assert.Equal(2, runs.First.NoticesEnqueued);
                Assert.Equal(2, runs.First.OrdersCancelled);
                Assert.Equal(1, runs.First.Refunded);
                Assert.Equal(0, runs.First.RefundFailures);
                Assert.Equal(1, runs.First.TemplatesPaused);
                Assert.Equal(2, runs.First.MembershipsCancelled);
                Assert.Equal(0, runs.First.CreditAccountsDischarged);
                Assert.Equal(0, runs.First.PeriodsClosed);

                // Notices: one per live customer and per approved cleaner, straight through the queue.
                var notices = _queue.Sent.Where(m => m.Queue == QueueNames.SendEmail).Select(m => m.Envelope).ToList();
                Assert.Equal(2, notices.Count);
                var customerNotice = Assert.Single(notices, n => n.Payload.EmailType == EmailType.CompanyWindDownCustomer);
                Assert.Equal(seeded.ConfirmedCustomerId, customerNotice.Payload.UserId);
                Assert.Equal(Code, customerNotice.Payload.Code);
                Assert.Equal(B, customerNotice.TenantId);
                Assert.Equal(MessageKeys.Email(EmailType.CompanyWindDownCustomer, seeded.ConfirmedCustomerId, MessageKeys.HashCode(Code)), customerNotice.MessageKey);
                var cleanerNotice = Assert.Single(notices, n => n.Payload.EmailType == EmailType.CompanyWindDownCleaner);
                Assert.Equal(seeded.ApprovedCleanerId, cleanerNotice.Payload.UserId);
                Assert.Equal(Code, cleanerNotice.Payload.Code);
                var progress = await ctx.Set<CampaignProgressRow>().SingleAsync(p => p.CampaignId == $"wind-down-notice:{B}:{Code}");
                Assert.True(progress.IsComplete);

                // Orders: the two paid-or-cash bookings on or after the date, and nothing else.
                var orders = await ctx.Orders.IgnoreQueryFilters().ToDictionaryAsync(o => o.Id);
                foreach (var id in new[] { seeded.AfterCardPaidOrderId, seeded.AfterCashOrderId })
                {
                    Assert.Equal(OrderStatus.Cancelled, orders[id].CurrentStatus);
                    Assert.Equal(CancelledBy.System, orders[id].CancelledBy);
                    Assert.Equal(OrderCancellationReasons.CompanyWindDown, orders[id].CancellationReason);
                    Assert.Equal(0m, orders[id].CancellationFeeRate);
                }

                Assert.Equal(OrderStatus.New, orders[seeded.BeforeCashOrderId].CurrentStatus);
                Assert.Equal(OrderStatus.Confirmed, orders[seeded.BeforeCardPaidOrderId].CurrentStatus);
                Assert.Equal(OrderStatus.New, orders[seeded.AfterCardUnpaidOrderId].CurrentStatus);
                Assert.Equal(OrderStatus.Confirmed, orders[seeded.ACardPaidOrderId].CurrentStatus);
                Assert.Equal(PaymentStatus.Paid, orders[seeded.ACardPaidOrderId].PaymentStatus);

                // The refund: the full price under the ServiceNotRendered key, the payment flipped.
                var refund = Assert.Single(await ctx.Refunds.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(seeded.AfterCardPaidOrderId, refund.OrderId);
                Assert.Equal($"refund:{seeded.AfterCardPaidOrderId}:admin", refund.RefundKey);
                Assert.Equal(orders[seeded.AfterCardPaidOrderId].TotalPrice, refund.Amount);
                Assert.Equal(RefundReason.ServiceNotRendered, refund.Reason);
                Assert.Equal(RefundStatus.Succeeded, refund.Status);
                Assert.Equal(PaymentStatus.Refunded, orders[seeded.AfterCardPaidOrderId].PaymentStatus);
                Assert.Equal(1, _stripe.RefundCalls);
                Assert.Equal($"refund:{seeded.AfterCardPaidOrderId}:admin", _stripe.LastRefundKey);

                // The customer's push and the assigned cleaner's, as outbox rows under B.
                var pushes = await ctx.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
                    .ToListAsync();
                Assert.All(pushes, p => Assert.Equal(B, p.TenantId));
                Assert.Single(pushes, p => p.Body.Contains(NotificationEventCatalog.OrderRefunded) && p.Body.Contains(seeded.ConfirmedCustomerId));
                Assert.Single(pushes, p => p.Body.Contains(NotificationEventCatalog.OrderAssignmentCancelled) && p.Body.Contains(seeded.ApprovedCleanerId));

                // Templates: the active one paused, the paused one and A's left alone.
                var templates = await ctx.RecurringBookingTemplates.IgnoreQueryFilters().ToDictionaryAsync(t => t.Id);
                Assert.False(templates[seeded.ActiveTemplateId].IsActive);
                Assert.False(templates[seeded.PausedTemplateId].IsActive);
                Assert.True(templates[seeded.ATemplateId].IsActive);

                // Plus: both currencies cancelled at period end, once each, A's untouched.
                var memberships = await ctx.UserMemberships.IgnoreQueryFilters().ToDictionaryAsync(m => m.Id);
                Assert.NotNull(memberships[seeded.EurMembershipId].CancelledAt);
                Assert.NotNull(memberships[seeded.CzkMembershipId].CancelledAt);
                Assert.Null(memberships[seeded.AMembershipId].CancelledAt);
                Assert.Equal(
                    new[] { memberships[seeded.CzkMembershipId].StripeSubscriptionId, memberships[seeded.EurMembershipId].StripeSubscriptionId }.Order().ToList(),
                    _stripe.CancelledSubscriptions.Order().ToList());

                // Credit: untouched while the company still operates.
                var credit = await ctx.CreditAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == seeded.CreditAccountId);
                Assert.Equal(300m, credit.Balance);

                var tenant = await ctx.Tenants.SingleAsync(t => t.Id == B);
                Assert.Null(tenant.WindDownRunStartedOn);
                Assert.NotNull(tenant.WindDownLastRunOn);

                // TC-LC-WD-2: the second delivery moved nothing.
                Assert.True(runs.Second.Ran);
                Assert.Equal(0, runs.Second.NoticesEnqueued);
                Assert.Equal(0, runs.Second.OrdersCancelled);
                Assert.Equal(0, runs.Second.Refunded);
                Assert.Equal(0, runs.Second.RefundsRedriven);
                Assert.Equal(0, runs.Second.TemplatesPaused);
                Assert.Equal(0, runs.Second.MembershipsCancelled);
                Assert.Equal(2, _queue.Sent.Count);
                Assert.Equal(1, _stripe.RefundCalls);
                Assert.Equal(2, _stripe.CancelledSubscriptions.Count);
                Assert.Single(await ctx.Refunds.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(2, (await ctx.Set<OrderStatusTrack>().IgnoreQueryFilters().CountAsync(t => t.Status == OrderStatus.Cancelled)));
                Assert.NotNull(runs.LastRunAfterFirst);
                Assert.True(tenant.WindDownLastRunOn > runs.LastRunAfterFirst);

                // The first run cancelled and refunded, so B's two administrators were told once, with
                // the four counts; the second run moved nothing and told nobody, and A's heard nothing.
                var told = await ctx.Set<UserNotification>().IgnoreQueryFilters()
                    .Where(n => n.EventKey == AdminNotificationEventCatalog.CompanyWindDownRun)
                    .OrderBy(n => n.UserId)
                    .ToListAsync();
                Assert.Equal(2, told.Count);
                Assert.All(told, row =>
                {
                    Assert.Equal(B, row.TenantId);
                    var args = JsonSerializer.Deserialize<Dictionary<string, string>>(row.ArgsJson)!;
                    Assert.Equal("2", args["cancelled"]);
                    Assert.Equal("1", args["refunded"]);
                    Assert.Equal("0", args["refundFailures"]);
                    Assert.Equal("0", args["periodsClosed"]);
                });
                var adminIds = await ctx.Users.IgnoreQueryFilters()
                    .Where(u => u.Profile == UserProfile.Administrator)
                    .Select(u => new { u.Id, u.TenantId })
                    .ToListAsync();
                Assert.Equal(adminIds.Where(a => a.TenantId == B).Select(a => a.Id).Order(), told.Select(t => t.UserId).Order());
                var runEmails = await ctx.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.SendEmail && m.Body.Contains(AdminNotificationEventCatalog.CompanyWindDownRun))
                    .ToListAsync();
                Assert.Equal(2, runEmails.Count);
                Assert.Equal(2, runEmails.Select(m => m.MessageKey).Distinct(StringComparer.Ordinal).Count());
                Assert.All(runEmails, m => Assert.Equal(B, m.TenantId));
            },
            transactional: false);
    }

    [Fact]
    public async Task A_refund_Stripe_refuses_stays_pending_and_the_next_run_re_drives_it_once_on_the_same_key()
    {
        Seeded seeded = default!;
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                seeded = await SeedTwoCompaniesAsync(ctx, deactivateB: false);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                _stripe.RefuseRefunds = true;
                var first = await RunSweepAsync(provider, B);
                var afterFirst = await ReadRefundAsync(provider, seeded.AfterCardPaidOrderId);
                var callsAfterFirst = _stripe.RefundCalls;

                _stripe.RefuseRefunds = false;
                var second = await RunSweepAsync(provider, B);
                var callsAfterSecond = _stripe.RefundCalls;

                var third = await RunSweepAsync(provider, B);
                var callsAfterThird = _stripe.RefundCalls;

                return (First: first, AfterFirst: afterFirst, CallsAfterFirst: callsAfterFirst, Second: second, CallsAfterSecond: callsAfterSecond, Third: third, CallsAfterThird: callsAfterThird);
            },
            assert: async (ctx, runs) =>
            {
                Assert.Equal(2, runs.First.OrdersCancelled);
                Assert.Equal(0, runs.First.Refunded);
                Assert.Equal(1, runs.First.RefundFailures);
                Assert.Equal(1, runs.CallsAfterFirst);
                Assert.Equal(RefundStatus.Pending, runs.AfterFirst.Status);
                Assert.Equal(PaymentStatus.Paid, runs.AfterFirst.PaymentStatus);
                Assert.Equal(OrderStatus.Cancelled, runs.AfterFirst.OrderStatus);

                Assert.Equal(0, runs.Second.OrdersCancelled);
                Assert.Equal(1, runs.Second.RefundsRedriven);
                Assert.Equal(0, runs.Second.RefundFailures);
                Assert.Equal(2, runs.CallsAfterSecond);
                Assert.True(_stripe.AllRefundKeysIdentical);

                Assert.Equal(0, runs.Third.RefundsRedriven);
                Assert.Equal(2, runs.CallsAfterThird);

                var refund = Assert.Single(await ctx.Refunds.IgnoreQueryFilters().ToListAsync());
                Assert.Equal($"refund:{seeded.AfterCardPaidOrderId}:admin", refund.RefundKey);
                Assert.Equal(RefundStatus.Succeeded, refund.Status);
                var order = await ctx.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == seeded.AfterCardPaidOrderId);
                Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
                Assert.Single(await ctx.OutboxMessages.IgnoreQueryFilters()
                    .Where(m => m.QueueName == QueueNames.NotificationsDispatch && m.Body.Contains(NotificationEventCatalog.OrderRefunded))
                    .ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task Once_the_door_is_closed_the_floor_lifts_credit_is_discharged_and_the_last_period_is_invoiced_without_a_successor()
    {
        Seeded seeded = default!;
        string openPeriodId = default!;
        string closedPeriodId = default!;
        string aPeriodId = default!;
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                seeded = await SeedTwoCompaniesAsync(ctx, deactivateB: true, includeOpenOrdersAfterDate: false);
                await ctx.CommitAsync(CancellationToken.None);
                (openPeriodId, closedPeriodId, aPeriodId) = await SeedPayrollAsync(ctx, seeded);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var sweep = await RunSweepAsync(provider, B);

                // A late pay row lands after the close (an InProgress order that completed): the
                // nightly rollover finds an expired Open period under each company.
                string bLatePeriodId = default!;
                await MutateAsync(provider, async ctx =>
                {
                    var late = NewOrder("b-completed-late", SvkId, EurId, seeded.ConfirmedCustomerId, WellBefore.AddDays(-3), PaymentType.Cash, PaymentStatus.Paid, B);
                    late.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, late));
                    late.MarkEmployeePayCalculated();
                    ctx.Orders.Add(late);
                    var expired = Stamped(PayPeriod.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-31)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))), B);
                    ctx.PayPeriods.Add(expired);
                    await ctx.CommitAsync(CancellationToken.None);
                    ctx.OrderEmployeePays.Add(Stamped(OrderEmployeePay.Create(late.Id, seeded.ApprovedCleanerEmployeeId, expired.Id, EurId, basePay: 20m, totalPay: 20m), B));
                    bLatePeriodId = expired.Id;
                });

                using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPayPeriodBackgroundService>()
                    .CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);
                return (Sweep: sweep, BLatePeriodId: bLatePeriodId);
            },
            assert: async (ctx, run) =>
            {
                var sweep = run.Sweep;
                Assert.True(sweep.Ran);
                Assert.Equal(2, sweep.OrdersCancelled);
                Assert.Equal(1, sweep.Refunded);
                Assert.Equal(1, sweep.CreditAccountsDischarged);
                Assert.Equal(1, sweep.PeriodsClosed);
                Assert.Null(sweep.PeriodStepSkippedBecause);

                // The floor is lifted: both bookings before the date go too, the card one with its money.
                var orders = await ctx.Orders.IgnoreQueryFilters().ToDictionaryAsync(o => o.Id);
                foreach (var id in new[] { seeded.BeforeCashOrderId, seeded.BeforeCardPaidOrderId })
                {
                    Assert.Equal(OrderStatus.Cancelled, orders[id].CurrentStatus);
                    Assert.Equal(CancelledBy.System, orders[id].CancelledBy);
                    Assert.Equal(OrderCancellationReasons.CompanyWindDown, orders[id].CancellationReason);
                }

                Assert.Equal(PaymentStatus.Refunded, orders[seeded.BeforeCardPaidOrderId].PaymentStatus);
                Assert.Equal(OrderStatus.Confirmed, orders[seeded.ACardPaidOrderId].CurrentStatus);

                // Credit: the positive balance discharged with the note, the empty one left alone.
                var accounts = await ctx.CreditAccounts.IgnoreQueryFilters().Include(a => a.Transactions).ToDictionaryAsync(a => a.Id);
                Assert.Equal(0m, accounts[seeded.CreditAccountId].Balance);
                var discharge = Assert.Single(accounts[seeded.CreditAccountId].Transactions, t => t.Reason == CreditTransactionReason.Expired);
                Assert.Equal(-300m, discharge.Amount);
                Assert.Equal("company wind-down", discharge.Note);
                Assert.Equal(AdminBId, discharge.CreatedBy);
                Assert.Empty(accounts[seeded.EmptyCreditAccountId].Transactions);

                // The last period: closed, one invoice per cleaner-currency with a PDF, the e-mail sent, no successor.
                var periods = await ctx.PayPeriods.IgnoreQueryFilters().ToListAsync();
                var open = periods.Single(p => p.Id == openPeriodId);
                Assert.Equal(PayPeriodStatus.Closed, open.Status);
                Assert.Equal("Company wind-down", open.Notes);

                var invoices = await ctx.EmployeeInvoices.IgnoreQueryFilters().Where(i => i.TenantId == B).ToListAsync();
                var invoice = Assert.Single(invoices, i => i.PayPeriodId == openPeriodId);
                Assert.Equal(seeded.ApprovedCleanerEmployeeId, invoice.EmployeeId);
                Assert.Equal(EurId, invoice.CurrencyId);
                Assert.False(string.IsNullOrEmpty(invoice.PdfBlobUrl));
                Assert.Single(_email.PeriodClosedSends, s => s.PeriodLabel == open.GetPeriodLabel() && s.InvoiceFileName == $"{invoice.InvoiceNumber}.pdf");

                // A Closed period holding an uninvoiced pay row is the page's fact, not the sweep's.
                Assert.DoesNotContain(invoices, i => i.PayPeriodId == closedPeriodId);
                Assert.DoesNotContain(_email.PeriodClosedSends, s => s.PeriodLabel == periods.Single(p => p.Id == closedPeriodId).GetPeriodLabel());

                // The rollover closed and invoiced both companies' expired periods, opened A's successor
                // and none for B.
                var aPeriods = periods.Where(p => p.TenantId == A).ToList();
                Assert.Equal(PayPeriodStatus.Closed, aPeriods.Single(p => p.Id == aPeriodId).Status);
                Assert.Single(aPeriods, p => p.Status == PayPeriodStatus.Open);
                Assert.Single(await ctx.EmployeeInvoices.IgnoreQueryFilters().Where(i => i.PayPeriodId == aPeriodId).ToListAsync());
                var bLate = periods.Single(p => p.Id == run.BLatePeriodId);
                Assert.Equal(PayPeriodStatus.Closed, bLate.Status);
                Assert.Single(invoices, i => i.PayPeriodId == run.BLatePeriodId);
                Assert.DoesNotContain(periods, p => p.TenantId == B && p.Status == PayPeriodStatus.Open);
                Assert.Equal(3, periods.Count(p => p.TenantId == B));
            },
            transactional: false);
    }

    [Fact]
    public async Task The_period_step_waits_for_an_order_under_way_and_for_a_pay_row_still_to_be_written()
    {
        Seeded seeded = default!;
        string inProgressOrderId = default!;
        await TestMethod(
            setup: SetupAsync,
            arrange: async ctx =>
            {
                seeded = await SeedTwoCompaniesAsync(ctx, deactivateB: true, includeOpenOrdersAfterDate: false, includeBeforeOrders: false);
                await ctx.CommitAsync(CancellationToken.None);
                await SeedPayrollAsync(ctx, seeded);
                var inProgress = NewOrder("b-in-progress", SvkId, EurId, seeded.ConfirmedCustomerId, WellBefore, PaymentType.Cash, PaymentStatus.Pending, B);
                inProgress.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, inProgress));
                inProgress.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, inProgress));
                ctx.Orders.Add(inProgress);
                inProgressOrderId = inProgress.Id;
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var withOrderUnderWay = await RunSweepAsync(provider, B);

                await MutateAsync(provider, async ctx =>
                {
                    var order = await ctx.Orders.IgnoreQueryFilters().Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == inProgressOrderId);
                    order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
                });
                var withPayOutstanding = await RunSweepAsync(provider, B);

                await MutateAsync(provider, async ctx =>
                {
                    var order = await ctx.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == inProgressOrderId);
                    order.MarkEmployeePayCalculated();
                });
                var settled = await RunSweepAsync(provider, B);

                return (WithOrderUnderWay: withOrderUnderWay, WithPayOutstanding: withPayOutstanding, Settled: settled);
            },
            assert: async (ctx, runs) =>
            {
                Assert.True(runs.WithOrderUnderWay.Ran);
                Assert.Equal(0, runs.WithOrderUnderWay.OrdersCancelled);
                Assert.Equal(0, runs.WithOrderUnderWay.PeriodsClosed);
                Assert.Equal("an order is still open", runs.WithOrderUnderWay.PeriodStepSkippedBecause);

                Assert.Equal(0, runs.WithPayOutstanding.PeriodsClosed);
                Assert.Equal("a completed order still awaits its pay calculation", runs.WithPayOutstanding.PeriodStepSkippedBecause);

                Assert.Equal(1, runs.Settled.PeriodsClosed);
                Assert.Null(runs.Settled.PeriodStepSkippedBecause);

                var order = await ctx.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == inProgressOrderId);
                Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
                Assert.Null(order.CancelledBy);
                Assert.DoesNotContain(await ctx.PayPeriods.IgnoreQueryFilters().ToListAsync(), p => p.TenantId == B && p.Status == PayPeriodStatus.Open);
            },
            transactional: false);
    }

    private static async Task<Seeded> SeedTwoCompaniesAsync(
        CleansiaDbContext ctx, bool deactivateB, bool includeOpenOrdersAfterDate = true, bool includeBeforeOrders = true)
    {
        ctx.Languages.Add(Language.Create("en", "English"));
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = EurId;
        eur.IsActive = true;
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        ctx.Currencies.AddRange(eur, czk);

        var slovakia = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        slovakia.Id = SvkId;
        var czechia = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        czechia.Id = CzeId;
        ctx.Countries.AddRange(slovakia, czechia);
        ctx.CountryConfigurations.AddRange(
            CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m, timeZoneId: "Europe/Bratislava").AssignOperator(B),
            CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m, timeZoneId: "Europe/Prague").AssignOperator(A).SetAsDefaultMarket(true));

        var plan = MembershipPlan.Create(PlanId, "Wind-down plan", 10m, 24, allowsExpressUpgrade: false);
        plan.Id = PlanId;
        ctx.MembershipPlans.Add(plan);

        var companyInfo = CompanyInfo.Create("Cleansia SK s.r.o.", "Cleansia SK", "12345678", "Hlavna 1", "Bratislava", "81101", SvkId);
        companyInfo.TenantId = B;
        ctx.Add(companyInfo);

        var registry = await ctx.Tenants.SingleAsync(t => t.Id == B);
        registry.RequestWindDown(WindDownFrom, AdminBId, RequestedOn);
        if (deactivateB)
        {
            registry.Deactivate(AdminBId, RequestedOn.AddDays(1));
        }

        // B's people: a confirmed customer, an unconfirmed one, an erased one, an approved cleaner, a
        // rejected applicant — and A's confirmed customer, who must hear nothing.
        var confirmedCustomer = Stamped(NewUser("wd-customer-b@cleansia.test", UserProfile.Customer, confirmed: true), B);
        var unconfirmedCustomer = Stamped(NewUser("wd-unconfirmed-b@cleansia.test", UserProfile.Customer, confirmed: false), B);
        var erasedCustomer = Stamped(NewUser("wd-erased-b@cleansia.test", UserProfile.Customer, confirmed: true), B);
        erasedCustomer.Anonymize();
        var approvedCleanerUser = Stamped(NewUser("wd-cleaner-b@cleansia.test", UserProfile.Employee, confirmed: true), B);
        var rejectedApplicantUser = Stamped(NewUser("wd-rejected-b@cleansia.test", UserProfile.Employee, confirmed: true), B);
        var plusCustomer = Stamped(NewUser("wd-plus-czk-b@cleansia.test", UserProfile.Customer, confirmed: false), B);
        var aCustomer = Stamped(NewUser("wd-customer-a@cleansia.test", UserProfile.Customer, confirmed: true), A);
        // B's two administrators hear of a run that moved something; A's hears nothing. None of them
        // is a notice recipient — the notices go to customers and approved cleaners only.
        var adminB1 = Stamped(NewUser("wd-admin-b1@cleansia.test", UserProfile.Administrator, confirmed: true), B);
        var adminB2 = Stamped(NewUser("wd-admin-b2@cleansia.test", UserProfile.Administrator, confirmed: true), B);
        var adminA = Stamped(NewUser("wd-admin-a@cleansia.test", UserProfile.Administrator, confirmed: true), A);
        ctx.Users.AddRange(confirmedCustomer, unconfirmedCustomer, erasedCustomer, approvedCleanerUser, rejectedApplicantUser, plusCustomer, aCustomer, adminB1, adminB2, adminA);

        var approvedCleaner = Employee.CreateWithUser(approvedCleanerUser).Approve(AdminBId);
        approvedCleaner.TenantId = B;
        var rejectedApplicant = Employee.CreateWithUser(rejectedApplicantUser).Reject(AdminBId, "no");
        rejectedApplicant.TenantId = B;
        ctx.AddRange(approvedCleaner, rejectedApplicant);

        // B's orders around the cut-off, and A's twin after it.
        var beforeCash = NewOrder("b-before-cash", SvkId, EurId, confirmedCustomer.Id, JustBeforeCutoff, PaymentType.Cash, PaymentStatus.Pending, B);
        var beforeCardPaid = NewOrder("b-before-card-paid", SvkId, EurId, confirmedCustomer.Id, WellBefore, PaymentType.Card, PaymentStatus.Paid, B);
        beforeCardPaid.AssignStripeSessionId("cs_wd_before");
        beforeCardPaid.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, beforeCardPaid));
        var afterCardPaid = NewOrder("b-after-card-paid", SvkId, EurId, confirmedCustomer.Id, AfterCutoff, PaymentType.Card, PaymentStatus.Paid, B);
        afterCardPaid.AssignStripeSessionId("cs_wd_after");
        afterCardPaid.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, afterCardPaid));
        afterCardPaid.SetMaxEmployees(1);
        afterCardPaid.AddAssignedEmployee(OrderEmployee.Create(afterCardPaid, approvedCleaner));
        var afterCash = NewOrder("b-after-cash", SvkId, EurId, confirmedCustomer.Id, LaterAfterCutoff, PaymentType.Cash, PaymentStatus.Pending, B);
        var afterCardUnpaid = NewOrder("b-after-card-unpaid", SvkId, EurId, confirmedCustomer.Id, AfterCutoff.AddDays(1), PaymentType.Card, PaymentStatus.Pending, B);
        var aCardPaid = NewOrder("a-after-card-paid", CzeId, CzkId, aCustomer.Id, AfterCutoff, PaymentType.Card, PaymentStatus.Paid, A);
        aCardPaid.AssignStripeSessionId("cs_wd_a");
        aCardPaid.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, aCardPaid));
        if (includeBeforeOrders)
        {
            ctx.Orders.AddRange(beforeCash, beforeCardPaid);
        }

        if (includeOpenOrdersAfterDate)
        {
            ctx.Orders.AddRange(afterCardPaid, afterCash, afterCardUnpaid);
        }

        ctx.Orders.Add(aCardPaid);

        // Templates, Plus in two currencies, credit — with A's twins.
        var (activeAddress, activeSaved) = SavedAddressFor(confirmedCustomer.Id, SvkId, B);
        var (pausedAddress, pausedSaved) = SavedAddressFor(plusCustomer.Id, SvkId, B);
        var (aAddress, aSaved) = SavedAddressFor(aCustomer.Id, CzeId, A);
        ctx.Addresses.AddRange(activeAddress, pausedAddress, aAddress);
        ctx.SavedAddresses.AddRange(activeSaved, pausedSaved, aSaved);
        var activeTemplate = Stamped(Template(confirmedCustomer.Id, activeSaved.Id), B);
        var pausedTemplate = Stamped(Template(plusCustomer.Id, pausedSaved.Id).Pause(), B);
        var aTemplate = Stamped(Template(aCustomer.Id, aSaved.Id), A);
        ctx.RecurringBookingTemplates.AddRange(activeTemplate, pausedTemplate, aTemplate);

        var eurMembership = Stamped(UserMembership.Create(confirmedCustomer.Id, PlanId, EurId, "sub_wd_eur", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(27)), B);
        var czkMembership = Stamped(UserMembership.Create(plusCustomer.Id, PlanId, CzkId, "sub_wd_czk", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(27)), B);
        var aMembership = Stamped(UserMembership.Create(aCustomer.Id, PlanId, CzkId, "sub_wd_a", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(27)), A);
        ctx.UserMemberships.AddRange(eurMembership, czkMembership, aMembership);

        var credit = Stamped(CreditAccount.Create(confirmedCustomer.Id, EurId, "seed"), B);
        credit.Issue(300m, CreditTransactionReason.Goodwill, "wd-credit:goodwill", "seed");
        var emptyCredit = Stamped(CreditAccount.Create(plusCustomer.Id, EurId, "seed"), B);
        ctx.CreditAccounts.AddRange(credit, emptyCredit);

        StampUnstampedAdded(ctx, B);
        return new Seeded(
            confirmedCustomer.Id,
            approvedCleanerUser.Id,
            approvedCleaner.Id,
            beforeCash.Id,
            beforeCardPaid.Id,
            afterCardPaid.Id,
            afterCash.Id,
            afterCardUnpaid.Id,
            aCardPaid.Id,
            activeTemplate.Id,
            pausedTemplate.Id,
            aTemplate.Id,
            eurMembership.Id,
            czkMembership.Id,
            aMembership.Id,
            credit.Id,
            emptyCredit.Id,
            aCustomer.Id);
    }

    /// <summary>
    /// B: an Open period holding one uninvoiced pay row on a completed, pay-calculated order, and a
    /// Closed period holding another uninvoiced row the sweep must not touch. A: an expired Open period
    /// with a pay row of its own, for the rollover to close, invoice and succeed.
    /// </summary>
    private static async Task<(string OpenPeriodId, string ClosedPeriodId, string APeriodId)> SeedPayrollAsync(CleansiaDbContext ctx, Seeded seeded)
    {
        var completed = NewOrder("b-completed-paid", SvkId, EurId, seeded.ConfirmedCustomerId, WellBefore.AddDays(-5), PaymentType.Cash, PaymentStatus.Paid, B);
        completed.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, completed));
        completed.MarkEmployeePayCalculated();
        var earlier = NewOrder("b-completed-earlier", SvkId, EurId, seeded.ConfirmedCustomerId, WellBefore.AddDays(-40), PaymentType.Cash, PaymentStatus.Paid, B);
        earlier.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, earlier));
        earlier.MarkEmployeePayCalculated();
        ctx.Orders.AddRange(completed, earlier);

        var open = Stamped(PayPeriod.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))), B);
        var closed = Stamped(PayPeriod.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-50)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-21))).Close("admin", "settled"), B);
        var aExpired = Stamped(PayPeriod.Create(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-31)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))), A);
        ctx.PayPeriods.AddRange(open, closed, aExpired);

        var aCleanerUser = Stamped(NewUser("wd-cleaner-a@cleansia.test", UserProfile.Employee, confirmed: true), A);
        ctx.Users.Add(aCleanerUser);
        var aCleaner = Employee.CreateWithUser(aCleanerUser).Approve("admin-a");
        aCleaner.TenantId = A;
        ctx.Add(aCleaner);
        var aCompleted = NewOrder("a-completed-paid", CzeId, CzkId, seeded.ACustomerId, WellBefore.AddDays(-5), PaymentType.Cash, PaymentStatus.Paid, A);
        aCompleted.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, aCompleted));
        aCompleted.MarkEmployeePayCalculated();
        ctx.Orders.Add(aCompleted);
        var aCompanyInfo = CompanyInfo.Create("Cleansia CZ s.r.o.", "Cleansia CZ", "87654321", "Hlavni 1", "Praha", "11000", CzeId);
        aCompanyInfo.TenantId = A;
        ctx.Add(aCompanyInfo);

        StampUnstampedAdded(ctx, B);
        await ctx.CommitAsync(CancellationToken.None);

        ctx.OrderEmployeePays.AddRange(
            Stamped(OrderEmployeePay.Create(completed.Id, seeded.ApprovedCleanerEmployeeId, open.Id, EurId, basePay: 60m, totalPay: 60m), B),
            Stamped(OrderEmployeePay.Create(earlier.Id, seeded.ApprovedCleanerEmployeeId, closed.Id, EurId, basePay: 40m, totalPay: 40m), B),
            Stamped(OrderEmployeePay.Create(aCompleted.Id, aCleaner.Id, aExpired.Id, CzkId, basePay: 500m, totalPay: 500m), A));
        return (open.Id, closed.Id, aExpired.Id);
    }

    private static T Stamped<T>(T entity, string tenantId) where T : Core.Domain.Common.ITenantEntity
    {
        entity.TenantId = tenantId;
        return entity;
    }

    private static User NewUser(string email, UserProfile profile, bool confirmed)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Wind", "Down", profile, adminRole: profile == UserProfile.Administrator ? AdminRole.Administrator : null);
        if (confirmed)
        {
            user.ConfirmEmail();
        }

        return user;
    }

    private static Order NewOrder(
        string id, string countryId, string currencyId, string userId, DateTime cleaningAt,
        PaymentType paymentType, PaymentStatus paymentStatus, string tenantId)
    {
        var address = Address.Create("Hlavna 1", "Bratislava", "81101", countryId);
        address.TenantId = tenantId;
        var order = Order.Create(
            customerName: "Wind Down",
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+421900000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningAt,
            paymentType: paymentType,
            totalPrice: 100m,
            currencyId: currencyId,
            paymentStatus: paymentStatus,
            userId: userId);
        order.Id = id;
        order.TenantId = tenantId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private static (Address Address, SavedAddress Saved) SavedAddressFor(string userId, string countryId, string tenantId)
    {
        var address = Address.Create("Obchodna 2", "Bratislava", "81106", countryId);
        address.TenantId = tenantId;
        var saved = SavedAddress.Create(userId, address.Id, "Home", isDefault: false);
        saved.TenantId = tenantId;
        return (address, saved);
    }

    private static RecurringBookingTemplate Template(string userId, string savedAddressId) =>
        RecurringBookingTemplate.Create(
            userId, RecurrenceFrequency.Weekly, System.DayOfWeek.Monday, new TimeOnly(9, 0), 1, 1, savedAddressId,
            [], [], PaymentType.Cash, DateTime.UtcNow.AddDays(-7));

    private static async Task MutateAsync(IServiceProvider provider, Func<CleansiaDbContext, Task> mutate)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        scope.ServiceProvider.GetRequiredService<Core.Domain.Repositories.ITenantProvider>().SetTenantOverride(B);
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        await mutate(ctx);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private static async Task<DateTimeOffset?> LastRunOnAsync(IServiceProvider provider)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        return await ctx.Tenants.AsNoTracking().Where(t => t.Id == B).Select(t => t.WindDownLastRunOn).SingleAsync();
    }

    private sealed record RefundSnapshot(RefundStatus Status, PaymentStatus PaymentStatus, OrderStatus OrderStatus);

    private static async Task<RefundSnapshot> ReadRefundAsync(IServiceProvider provider, string orderId)
    {
        using var scope = provider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        var refund = await ctx.Refunds.IgnoreQueryFilters().SingleAsync(r => r.OrderId == orderId);
        var order = await ctx.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        return new RefundSnapshot(refund.Status, order.PaymentStatus, order.CurrentStatus);
    }

    private static IPdfService StubPdfService()
    {
        var pdf = new Mock<IPdfService>();
        pdf.Setup(p => p.GenerateInvoicePdf(It.IsAny<InvoicePdfData>(), It.IsAny<CountryInvoiceContext?>(), It.IsAny<string?>()))
            .Returns([0x25, 0x50, 0x44, 0x46]);
        return pdf.Object;
    }

    private static IBlobContainerClientFactory StubBlobFactory()
    {
        var container = new Mock<IBlobContainerClient>();
        container.Setup(c => c.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Core.Blobs.Abstractions.Extensions.Metadata?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        container.Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns((string name) => new Uri($"https://blobs.test/generated-invoices/{name}"));
        var factory = new Mock<IBlobContainerClientFactory>();
        factory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(container.Object);
        return factory.Object;
    }

    private sealed class StubStripeClientFactory(IStripeClient client) : IStripeClientFactory
    {
        public IStripeClient CreateClient() => client;
    }

    private sealed class RecordingQueueClient : IQueueClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public List<(string Queue, QueueEnvelope<SendEmailMessage> Envelope)> Sent { get; } = [];

        public Task SendAsync<T>(string queueName, T message, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(message, JsonOptions);
            Sent.Add((queueName, JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(json, JsonOptions)!));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<(string Email, string PeriodLabel, string? InvoiceFileName)> PeriodClosedSends { get; } = [];

        private static Task<string> Sent() => Task.FromResult("test-message-id");

        public Task<string> SendPeriodClosedEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, DateTime closedAt, string periodLabel, string languageCode = "en", byte[]? invoicePdfBytes = null, string? invoiceFileName = null, CancellationToken ct = default)
        {
            PeriodClosedSends.Add((email, periodLabel, invoiceFileName));
            return Sent();
        }

        public Task<string> SendResetPasswordEmailAsync(string email, string fullUserName, string code, string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendOrderReceiptEmailAsync(string email, Order order, byte[]? pdfBytes = null, string fileName = "receipt.pdf", string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendTestOrderReceiptEmailAsync(string email, string customerName, string orderNumber, string orderDate, string totalAmount, string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendEmailConfirmationAsync(string email, string userName, string verificationCode, string languageCode, CancellationToken ct = default) => Sent();
        public Task<string> SendPeriodEndReminderEmailAsync(string email, string employeeName, DateOnly startDate, DateOnly endDate, int daysRemaining, string periodLabel, string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendPromoCodeEmailAsync(string email, string promoCode, string discountLabel, DateTime? expiresOn, string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendOrderStatusUpdateEmailAsync(string email, Order order, string newStatus, string languageCode = "en", CancellationToken ct = default, decimal? refundedAmount = null) => Sent();
        public Task<string> SendCompanyWindDownCustomerNoticeAsync(string email, string userName, IReadOnlyList<string> companyNames, DateOnly windDownFrom, string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendCompanyWindDownCleanerNoticeAsync(string email, string userName, IReadOnlyList<string> companyNames, DateOnly windDownFrom, string languageCode = "en", CancellationToken ct = default) => Sent();
        public Task<string> SendAdminNotificationEmailAsync(string email, string eventKey, IReadOnlyDictionary<string, string> args, string languageCode = "en", CancellationToken ct = default) => Sent();
    }

    /// <summary>Refunds and subscription cancels are recorded; anything else on this path is a bug.</summary>
    private sealed class RecordingStripeClient : IStripeClient
    {
        private readonly List<string> _refundKeys = [];

        public int RefundCalls { get; private set; }
        public string? LastRefundKey { get; private set; }
        public bool RefuseRefunds { get; set; }
        public List<string> CancelledSubscriptions { get; } = [];
        public bool AllRefundKeysIdentical => _refundKeys.Distinct().Count() <= 1;

        public Task RefundCheckoutSessionAsync(string stripeSessionId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
            => RecordRefund(idempotencyKey);

        public Task RefundPaymentIntentAsync(string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
            => RecordRefund(idempotencyKey);

        private Task RecordRefund(string idempotencyKey)
        {
            RefundCalls++;
            if (RefuseRefunds)
            {
                throw new StripeException("simulated Stripe refund refusal");
            }

            _refundKeys.Add(idempotencyKey);
            LastRefundKey = idempotencyKey;
            return Task.CompletedTask;
        }

        public Task CancelSubscriptionAtPeriodEndAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
        {
            CancelledSubscriptions.Add(stripeSubscriptionId);
            return Task.CompletedTask;
        }

        public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> CreateCustomerAsync(string userId, string email, string fullName, string? phone, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PaymentIntentResult> CreatePaymentIntentAsync(decimal amount, string currency, string stripeCustomerId, string orderId, string displayOrderNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StripePaymentSnapshot> GetPaymentSnapshotAsync(string? stripeSessionId, string? stripePaymentIntentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> CreateEphemeralKeyAsync(string stripeCustomerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SetupIntentResult> CreateSetupIntentAsync(string stripeCustomerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionResult> CreateSubscriptionAsync(string stripeCustomerId, string stripePriceId, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SubscriptionResult> SwapSubscriptionPriceAsync(string stripeSubscriptionId, string newStripePriceId, string idempotencyAttemptId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> CreateMembershipCheckoutSessionAsync(string stripeCustomerId, string stripePriceId, string userId, string membershipPlanCode, int trialPeriodDays, string idempotencyAttemptId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
