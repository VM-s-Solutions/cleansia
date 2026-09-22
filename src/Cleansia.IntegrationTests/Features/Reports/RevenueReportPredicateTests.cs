using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Reports;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Reports;

/// <summary>
/// The revenue report's SET is decided in SQL: completed, paid at some point, completed inside the
/// period, one currency — and the two refund legs are summed per order over any date. These are the
/// repository predicates against real Postgres, plus the one path only the pipeline can prove: an
/// order the administrator's override completed is dated, so it is in the month it was completed in.
/// </summary>
[Collection("PostgresCollection")]
public class RevenueReportPredicateTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-rev";
    private const string Czk = "currency-czk-rev";
    private const string Eur = "currency-eur-rev";
    private const string AdminId = "admin-rev";

    private static readonly DateTime Start = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime March5 = new(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime April1 = new(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Task AdminSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, "admin-rev@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    // ── the completed-and-paid set ───────────────────────────────────

    [Fact]
    public async Task The_Set_Is_Completed_And_Paid_At_Some_Point_And_Nothing_Else()
    {
        await TestMethod(
            arrange: async context =>
            {
                SeedCatalogue(context);
                context.AddRange(
                    Completed("paid", PaymentStatus.Paid, March5),
                    Completed("partial", PaymentStatus.PartiallyRefunded, March5),
                    Completed("refunded", PaymentStatus.Refunded, March5),
                    Completed("unpaid-completed", PaymentStatus.Pending, March5),
                    At("new", OrderStatus.New),
                    At("confirmed", OrderStatus.Confirmed),
                    At("in-progress", OrderStatus.InProgress),
                    Cancelled("cancelled", March5));
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .GetCompletedPaidOrdersByCompletionDateAsync(Start, End, Czk, CancellationToken.None),
            assert: (CleansiaDbContext _, IReadOnlyList<Order> orders) =>
            {
                Assert.Equal(["paid", "partial", "refunded"], orders.Select(o => o.Id).Order());
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Period_Is_Inclusive_On_CompletedAt_And_An_Undated_Completed_Order_Is_Out()
    {
        await TestMethod(
            arrange: async context =>
            {
                SeedCatalogue(context);
                context.AddRange(
                    Completed("on-start", PaymentStatus.Paid, Start),
                    Completed("on-end", PaymentStatus.Paid, End),
                    Completed("after-end", PaymentStatus.Paid, End.AddSeconds(1)),
                    Completed("before-start", PaymentStatus.Paid, Start.AddSeconds(-1)),
                    Completed("undated", PaymentStatus.Paid, completedAt: null));
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .GetCompletedPaidOrdersByCompletionDateAsync(Start, End, Czk, CancellationToken.None),
            assert: (CleansiaDbContext _, IReadOnlyList<Order> orders) =>
            {
                Assert.Equal(["on-end", "on-start"], orders.Select(o => o.Id).Order());
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// Booked on 31 March, completed on 1 April: April's, not March's — the axis is the completion.
    /// And an order in another currency completed in the period is not in this currency's set.
    /// </summary>
    [Fact]
    public async Task The_Axis_Is_The_Completion_Date_And_The_Currency_Scopes_The_Set()
    {
        await TestMethod(
            arrange: async context =>
            {
                SeedCatalogue(context);
                context.AddRange(
                    Completed("booked-march-done-april", PaymentStatus.Paid, April1, cleaningDateTime: new DateTime(2026, 3, 31, 9, 0, 0, DateTimeKind.Utc)),
                    Completed("march-czk", PaymentStatus.Paid, March5),
                    Completed("march-eur", PaymentStatus.Paid, March5, currencyId: Eur));
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IOrderRepository>();
                var march = await repository.GetCompletedPaidOrdersByCompletionDateAsync(Start, End, Czk, CancellationToken.None);
                var april = await repository.GetCompletedPaidOrdersByCompletionDateAsync(April1.AddDays(-1).Date, April1.AddMonths(1), Czk, CancellationToken.None);
                return (March: march, April: april);
            },
            assert: (CleansiaDbContext _, (IReadOnlyList<Order> March, IReadOnlyList<Order> April) sets) =>
            {
                Assert.Equal("march-czk", Assert.Single(sets.March).Id);
                Assert.Equal("booked-march-done-april", Assert.Single(sets.April).Id);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// Only the pipeline can prove this: the override writes the Completed track without
    /// <c>CompleteOrder</c>, and before it stamped <c>CompletedAt</c> every order it completed was
    /// revenue of no month. The report is asked through the same mediator, in the override's window.
    /// </summary>
    [Fact]
    public async Task An_Order_The_Override_Completed_Is_In_The_Report_On_Its_Override_Date()
    {
        const string orderId = "override-completed";
        await TestMethod(
            setup: AdminSession,
            arrange: async context =>
            {
                SeedCatalogue(context);
                var order = At(orderId, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress);
                context.Add(order);
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var before = DateTime.UtcNow;
                var overridden = await mediator.Send(new AdminOverrideOrderStatus.Command(orderId, OrderStatus.Completed));
                Assert.True(overridden.IsSuccess, overridden.Error?.Message);
                var report = await mediator.Send(new GetRevenueReport.Query(
                    new ReportFilter(before.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1), Czk)));
                return (Before: before, Report: report);
            },
            assert: async (CleansiaDbContext context, (DateTime Before, BusinessResult<RevenueReportDto> Report) outcome) =>
            {
                var completedAt = await context.Orders.IgnoreQueryFilters()
                    .Where(o => o.Id == orderId)
                    .Select(o => o.CompletedAt)
                    .SingleAsync();
                Assert.NotNull(completedAt);
                Assert.InRange(completedAt!.Value, outcome.Before, DateTime.UtcNow);

                Assert.True(outcome.Report.IsSuccess, outcome.Report.Error?.Message);
                var report = outcome.Report.Value!;
                Assert.Equal(1, report.TotalOrders);
                Assert.Equal(1000m, report.NetRevenue);
                Assert.Equal(DateOnly.FromDateTime(completedAt.Value), Assert.Single(report.DailyRevenues).Date);
            },
            transactional: false);
    }

    // ── the two refund legs ──────────────────────────────────────────

    [Fact]
    public async Task Card_Refund_Totals_Sum_Succeeded_Rows_Only_Over_Any_Date()
    {
        await TestMethod(
            arrange: async context =>
            {
                SeedCatalogue(context);
                var twice = Completed("twice", PaymentStatus.PartiallyRefunded, March5);
                var pendingOnly = Completed("pending-only", PaymentStatus.Paid, March5);
                var untouched = Completed("untouched", PaymentStatus.Paid, March5);
                context.AddRange(twice, pendingOnly, untouched);

                var inMarch = Refund.Create(twice.Id, "refund:twice:1", 100m, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
                inMarch.MarkSucceeded("re_1", new DateTimeOffset(March5.AddDays(1)));
                var inApril = Refund.Create(twice.Id, "refund:twice:2", 50m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
                inApril.MarkSucceeded("re_2", new DateTimeOffset(April1));
                var pending = Refund.Create(pendingOnly.Id, "refund:pending:1", 300m, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
                var failed = Refund.Create(pendingOnly.Id, "refund:pending:2", 300m, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund);
                // Failed has no domain writer; set directly.
                typeof(Refund).GetProperty(nameof(Refund.Status))!.SetValue(failed, RefundStatus.Failed);
                context.AddRange(inMarch, inApril, pending, failed);
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IRefundRepository>()
                .GetSucceededRefundTotalsByOrderAsync(["twice", "pending-only", "untouched"], CancellationToken.None),
            assert: async (CleansiaDbContext context, IReadOnlyDictionary<string, decimal> totals) =>
            {
                Assert.Equal(150m, Assert.Single(totals).Value);
                Assert.Equal(150m, totals["twice"]);

                // The refund's currency is the order's by construction (RefundService denominates it
                // from order.Currency.Code); the fixture must not say otherwise.
                var mismatched = await context.Refunds.IgnoreQueryFilters()
                    .Where(r => r.Currency != r.Order!.Currency!.Code)
                    .CountAsync();
                Assert.Equal(0, mismatched);
            },
            transactional: false);
    }

    [Fact]
    public async Task Credit_Return_Totals_Sum_OrderPaymentReturned_Rows_Only_For_The_Orders_Asked()
    {
        await TestMethod(
            arrange: async context =>
            {
                SeedCatalogue(context);
                var customer = User.CreateWithPassword("credit-rev@cleansia.test", "Seed-Password-123", "Credit", "Rev");
                customer.Id = "user-credit-rev";
                context.Users.Add(customer);

                var mixed = Completed("mixed", PaymentStatus.Refunded, March5, userId: customer.Id);
                var abandoned = Cancelled("abandoned", cancelledAt: null, userId: customer.Id);
                context.AddRange(mixed, abandoned);

                var account = CreditAccount.Create(customer.Id, Czk, "seed");
                account.Issue(1000m, CreditTransactionReason.Goodwill, "credit:rev:grant", "seed");
                account.Issue(500m, CreditTransactionReason.OrderPaymentReturned, "credit:rev:mixed:return", "seed", orderId: mixed.Id);
                account.Issue(200m, CreditTransactionReason.OrderPaymentReturned, "credit:rev:abandoned:return", "seed", orderId: abandoned.Id);
                account.Issue(300m, CreditTransactionReason.DisputeSettlement, "credit:rev:mixed:dispute", "seed", orderId: mixed.Id);
                context.CreditAccounts.Add(account);
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<ICreditAccountRepository>()
                .GetReturnedTotalsByOrderAsync(["mixed"], CancellationToken.None),
            assert: (CleansiaDbContext _, IReadOnlyDictionary<string, decimal> totals) =>
            {
                Assert.Equal(500m, Assert.Single(totals).Value);
                Assert.Equal(500m, totals["mixed"]);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    // ── cancelled bookings ───────────────────────────────────────────

    /// <summary>
    /// A cancelled BOOKING carries <c>CancelledAt</c>; an abandoned card checkout carries a Cancelled
    /// track and no <c>CancelledAt</c> because it was never a booking. The count reads the column.
    /// </summary>
    [Fact]
    public async Task Cancelled_Bookings_Are_Counted_By_CancelledAt_And_Currency_Not_Abandoned_Checkouts()
    {
        await TestMethod(
            arrange: async context =>
            {
                SeedCatalogue(context);
                context.AddRange(
                    Cancelled("booking-march", March5),
                    Cancelled("booking-april", April1),
                    Cancelled("booking-eur", March5, currencyId: Eur),
                    Cancelled("abandoned-checkout", cancelledAt: null),
                    Completed("completed", PaymentStatus.Paid, March5));
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IOrderRepository>()
                .CountCancelledBookingsInPeriodAsync(Start, End, Czk, CancellationToken.None),
            assert: (CleansiaDbContext _, int count) =>
            {
                Assert.Equal(1, count);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    // ── arrangement ──────────────────────────────────────────────────

    private static void SeedCatalogue(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = Czk;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = Eur;
        eur.IsActive = true;
        context.Currencies.AddRange(czk, eur);
    }

    private static Order NewOrder(
        string id,
        PaymentStatus paymentStatus,
        string currencyId,
        DateTime? cleaningDateTime,
        string? userId)
    {
        var order = Order.Create(
            customerName: "Revenue Customer",
            customerEmail: $"{id}@cleansia.test",
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Revenue St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? March5.AddHours(-3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currencyId,
            paymentStatus: paymentStatus,
            userId: userId);
        order.Id = id;
        return order;
    }

    private static Order At(string id, params OrderStatus[] history)
    {
        var order = NewOrder(id, PaymentStatus.Paid, Czk, cleaningDateTime: null, userId: null);
        var stamp = DateTimeOffset.UtcNow.AddHours(-history.Length);
        foreach (var status in history)
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }

    private static Order Completed(
        string id,
        PaymentStatus paymentStatus,
        DateTime? completedAt,
        string currencyId = Czk,
        DateTime? cleaningDateTime = null,
        string? userId = null)
    {
        var order = NewOrder(id, paymentStatus, currencyId, cleaningDateTime, userId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        if (completedAt is not null)
        {
            order.MarkCompletedAt(completedAt.Value);
        }

        return order;
    }

    private static Order Cancelled(string id, DateTime? cancelledAt, string currencyId = Czk, string? userId = null)
    {
        var order = NewOrder(id, PaymentStatus.Pending, currencyId, cleaningDateTime: null, userId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        if (cancelledAt is not null)
        {
            order.Cancel(cancelledAt.Value, CancelledBy.Customer, feeRate: 0m, refundAmount: 0m, reason: null);
        }

        return order;
    }
}
