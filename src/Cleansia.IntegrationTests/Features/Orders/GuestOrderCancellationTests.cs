using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Infra.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Auditing;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

[Collection("PostgresCollection")]
public class GuestOrderCancellationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OrderId = "guest-cancel-order";
    private const string Email = "guest-cancel@example.test";
    private const string Ip = "203.0.113.75";
    private const string CleanerId = "guest-cancel-cleaner";
    private const string CurrencyId = "guest-cancel-eur";
    private const string CountryId = "guest-cancel-sk";

    private sealed class Run
    {
        public readonly Mock<IStripeClient> Stripe = new();
        public readonly Mock<IEmailService> EmailService = new();
        public readonly Dictionary<string, decimal> StripeRefunds = [];
        public Func<Task>? DuringStripe;
        public bool FailFinalCommit;
        public string? DeliveredTo;
        public decimal? DeliveredAmount;
        public int Deliveries;
        public bool FailEmail;

        public Task Setup(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider()));
            services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, "guest browser", null)));
            var factory = new Mock<IStripeClientFactory>();
            factory.Setup(x => x.CreateClient()).Returns(Stripe.Object);
            services.Replace(ServiceDescriptor.Singleton(factory.Object));
            Stripe.Setup(x => x.RefundPaymentIntentAsync("pi_guest", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (string _, decimal amount, string key, CancellationToken _) =>
                {
                    if (StripeRefunds.TryGetValue(key, out var original)) Assert.Equal(original, amount);
                    else StripeRefunds.Add(key, amount);
                    if (DuringStripe is not null) await DuringStripe();
                });
            EmailService.Setup(x => x.SendOrderStatusUpdateEmailAsync(It.IsAny<string>(), It.IsAny<Order>(),
                    "Cancelled", It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
                .Returns((string to, Order _, string _, string _, CancellationToken _, decimal? amount) =>
                {
                    if (FailEmail) throw new HttpRequestException("recording transport unavailable");
                    DeliveredTo = to;
                    DeliveredAmount = amount;
                    Deliveries++;
                    return Task.FromResult("sent");
                });
            services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => EmailService.Object));
            services.Replace(ServiceDescriptor.Scoped<IAuditWriter>(sp => new FailingFinalAuditWriter(
                new DbContextAuditWriter(sp.GetRequiredService<CleansiaDbContext>(), sp.GetRequiredService<ITenantProvider>()), this)));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingFinalAuditWriter(IAuditWriter inner, Run run) : IAuditWriter
    {
        public void Add(AdminActionAudit entry) => inner.Add(entry);
        public void Add(CustomerActionAudit entry)
        {
            if (run.FailFinalCommit && entry.Success)
            {
                run.FailFinalCommit = false;
                entry.TenantId = "missing-operator";
            }
            inner.Add(entry);
        }
    }

    private static async Task Seed(CleansiaDbContext db, bool paid = true, bool accepted = false,
        bool owned = false, OrderStatus status = OrderStatus.Confirmed, decimal priorRefund = 0m)
    {
        db.Languages.Add(Language.Create("en", "English"));
        var currency = Currency.Create("EUR", "€", "Euro");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        db.Currencies.Add(currency);
        var country = Country.Create("Slovakia", "SVK", "SK", true);
        country.Id = CountryId;
        db.Countries.Add(country);
        User? customer = null;
        if (owned)
        {
            customer = User.CreateWithPassword(Email, "Password123!", "Account", "Owner");
            db.Users.Add(customer);
        }
        var order = Order.Create("Guest", Email, "+421900123456",
            Address.Create("Guest Street", "Bratislava", "81101", CountryId), 2, 1,
            DateTime.UtcNow.AddHours(3), PaymentType.Card, 1000m, CurrencyId,
            paid ? PaymentStatus.Paid : PaymentStatus.Pending, userId: customer?.Id);
        order.Id = OrderId;
        order.TenantId = TestTenants.Second;
        order.CustomerAddress!.TenantId = TestTenants.Second;
        order.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        order.AssignStripePaymentIntentId("pi_guest");
        var track = OrderStatusTrack.Create(status, order);
        track.TenantId = TestTenants.Second;
        track.Created("seed", DateTimeOffset.UtcNow.AddDays(-1));
        order.AddOrderStatus(track);
        if (accepted)
        {
            var user = User.CreateWithPassword("cleaner-guest@example.test", "Password123!", "Clean", "Er", UserProfile.Employee);
            user.Id = CleanerId;
            user.TenantId = TestTenants.Second;
            var employee = Employee.CreateWithUser(user);
            employee.TenantId = TestTenants.Second;
            db.Employees.Add(employee);
            var assignment = OrderEmployee.Create(order, employee);
            order.AddAssignedEmployee(assignment);
        }
        db.Orders.Add(order);
        if (priorRefund > 0m)
        {
            var prior = Refund.Create(OrderId, "prior-refund", priorRefund, "EUR", RefundReason.CustomerCancellation, RefundSource.AppRefund);
            prior.TenantId = TestTenants.Second;
            prior.MarkSucceeded("re_prior", DateTimeOffset.UtcNow);
            db.Refunds.Add(prior);
        }
        StampUnstampedAdded(db, TestTenants.Default);
        await db.CommitAsync(CancellationToken.None);
    }

    private static async Task<CancelGuestOrder.Command> Command(IServiceProvider provider)
    {
        var order = await provider.GetRequiredService<CleansiaDbContext>().Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        return new(order.DisplayOrderNumber, Email.ToUpperInvariant(), order.ConfirmationCode.ToLowerInvariant(), "private reason");
    }

    private static async Task StartInSeparateScope(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
        var db = scope.ServiceProvider.GetRequiredService<CleansiaDbContext>();
        var order = await db.Orders.Include(o => o.OrderStatusHistory).SingleAsync(o => o.Id == OrderId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.InProgress, order));
        await db.CommitAsync(CancellationToken.None);
    }

    private static async Task AssertUncancelled(CleansiaDbContext db, OrderStatus status)
    {
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(status, order.CurrentStatus);
        Assert.Null(order.CancelledAt);
        Assert.Empty(await db.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.CustomerActionAudits.IgnoreQueryFilters().Where(x => x.Success).ToListAsync());
    }

    [Theory]
    [InlineData(false, 1000)]
    [InlineData(true, 500)]
    public async Task Preview_and_paid_cancel_share_standard_fees_and_stamp_the_proven_operator(bool accepted, int refund)
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, accepted: accepted),
            act: async (IServiceProvider provider) =>
            {
                var command = await Command(provider);
                var mediator = provider.GetRequiredService<IMediator>();
                var preview = await mediator.Send(new GetGuestCancellationFeePreview.Query(command.DisplayOrderNumber, command.Email, command.ConfirmationCode));
                Assert.True(preview.IsSuccess, preview.Error?.Message);
                Assert.Equal(refund, preview.Value.RefundAmount);
                using (var read = provider.CreateScope()) await AssertUncancelled(read.ServiceProvider.GetRequiredService<CleansiaDbContext>(), OrderStatus.Confirmed);
                var result = await mediator.Send(command);
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(preview.Value.FeeRate, result.Value.FeeRate);
                Assert.Equal(refund, result.Value.ActualRefundAmount);
                return true;
            }, assert: async (CleansiaDbContext db, bool _) =>
            {
                var order = await db.Orders.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
                Assert.Equal(CancelledBy.Customer, order.CancelledBy);
                Assert.All(await db.OrderStatusHistory.IgnoreQueryFilters().ToListAsync(), x => Assert.Equal(TestTenants.Second, x.TenantId));
                var money = Assert.Single(await db.Refunds.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(TestTenants.Second, money.TenantId);
                Assert.Equal(refund, money.Amount);
                var audit = Assert.Single(await db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.True(audit.Success);
                Assert.Null(audit.UserId);
                Assert.Equal(Ip, audit.IpAddress);
                Assert.Equal(OrderId, audit.ResourceId);
                Assert.Equal(TestTenants.Second, audit.TenantId);
                using var payload = JsonDocument.Parse(audit.PayloadJson!);
                Assert.Equal(refund, payload.RootElement.GetProperty("actualRefundAmount").GetDecimal());
                Assert.DoesNotContain("private reason", audit.PayloadJson);
                var outbox = await db.OutboxMessages.IgnoreQueryFilters().ToListAsync();
                Assert.All(outbox, x => Assert.Equal(TestTenants.Second, x.TenantId));
                var email = Assert.Single(outbox.Where(x => x.QueueName == QueueNames.SendEmail));
                Assert.DoesNotContain(Email, email.Body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(order.ConfirmationCode, email.Body);
                var notifications = await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
                if (accepted) Assert.Equal(CleanerId, Assert.Single(notifications).UserId);
                else Assert.Empty(notifications);
                Assert.Empty(await db.CreditAccounts.IgnoreQueryFilters().ToListAsync());
                Assert.Single(run.StripeRefunds);
            }, transactional: false);
    }

    [Fact]
    public async Task Unpaid_guest_cancel_does_not_create_an_account_or_claim_a_refund()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, paid: false), act: async (IServiceProvider provider) =>
        {
            var result = await provider.GetRequiredService<IMediator>().Send(await Command(provider));
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.False(result.Value.RefundInitiated);
            Assert.Null(result.Value.ActualRefundAmount);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Empty(await db.Refunds.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await db.CreditAccounts.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await db.Users.IgnoreQueryFilters().ToListAsync());
            Assert.Empty(await db.Set<UserNotification>().IgnoreQueryFilters().ToListAsync());
            Assert.Single(await db.OutboxMessages.IgnoreQueryFilters().Where(x => x.QueueName == QueueNames.SendEmail).ToListAsync());
        }, transactional: false);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("code")]
    [InlineData("number")]
    [InlineData("account")]
    public async Task Unproven_or_account_owned_keys_return_the_same_refusal(string mismatch)
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, owned: mismatch == "account"), act: async (IServiceProvider provider) =>
        {
            var command = await Command(provider);
            command = mismatch switch
            {
                "email" => command with { Email = "other@example.test" },
                "code" => command with { ConfirmationCode = "wrong" },
                "number" => command with { DisplayOrderNumber = "missing" },
                _ => command
            };
            var mediator = provider.GetRequiredService<IMediator>();
            var preview = await mediator.Send(new GetGuestCancellationFeePreview.Query(command.DisplayOrderNumber, command.Email, command.ConfirmationCode));
            Assert.Equal(BusinessErrorMessage.OrderNotFound, preview.Error?.Message);
            var result = await mediator.Send(command);
            Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error?.Message);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            await AssertUncancelled(db, OrderStatus.Confirmed);
            Assert.Empty(await db.Refunds.IgnoreQueryFilters().ToListAsync());
            var audit = Assert.Single(await db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
            Assert.False(audit.Success);
            Assert.Null(audit.UserId);
            Assert.Null(audit.ResourceId);
            Assert.Equal(BusinessErrorMessage.OrderNotFound, audit.ErrorCode);
        }, transactional: false);
    }

    [Fact]
    public async Task Started_order_refusal_retains_proven_operator_and_null_customer_audit()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, status: OrderStatus.InProgress), act: async (IServiceProvider provider) =>
        {
            var result = await provider.GetRequiredService<IMediator>().Send(await Command(provider));
            Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, result.Error?.Message);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            await AssertUncancelled(db, OrderStatus.InProgress);
            var audit = Assert.Single(await db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync());
            Assert.Equal(TestTenants.Second, audit.TenantId);
            Assert.Null(audit.UserId);
            Assert.Equal(OrderId, audit.ResourceId);
            Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, audit.ErrorCode);
        }, transactional: false);
    }

    [Fact]
    public async Task Actual_clamped_refund_is_carried_to_durable_email_and_dispatch_retries_without_losing_mail()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, priorRefund: 600m), act: async (IServiceProvider provider) =>
        {
            var result = await provider.GetRequiredService<IMediator>().Send(await Command(provider));
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(1000m, result.Value.RefundAmount);
            Assert.Equal(400m, result.Value.ActualRefundAmount);
            var body = await provider.GetRequiredService<CleansiaDbContext>().OutboxMessages.IgnoreQueryFilters()
                .Where(x => x.QueueName == QueueNames.SendEmail).Select(x => x.Body).SingleAsync();
            run.FailEmail = true;
            using (var send = provider.CreateScope())
                await Assert.ThrowsAsync<HttpRequestException>(() => ActivatorUtilities.CreateInstance<SendEmailHandler>(send.ServiceProvider).HandleAsync(body, CancellationToken.None));
            run.FailEmail = false;
            for (var i = 0; i < 2; i++)
            {
                using var send = provider.CreateScope();
                await ActivatorUtilities.CreateInstance<SendEmailHandler>(send.ServiceProvider).HandleAsync(body, CancellationToken.None);
            }
            Assert.Equal(Email, run.DeliveredTo);
            Assert.Equal(400m, run.DeliveredAmount);
            Assert.Equal(1, run.Deliveries);
            return true;
        }, transactional: false);
    }

    [Fact]
    public async Task Proven_guest_operator_precedes_default_market_resolution_and_validation()
    {
        var run = new Run();
        var resolver = new Mock<IOperatorTenantResolver>(MockBehavior.Strict);
        await TestMethod<bool>(setup: (IServiceCollection services) =>
        {
            run.Setup(services);
            services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp => new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
            services.Replace(ServiceDescriptor.Scoped<IOperatorTenantResolver>(_ => resolver.Object));
            return Task.CompletedTask;
        }, arrange: (CleansiaDbContext db) => Seed(db, paid: false), act: async (IServiceProvider provider) =>
        {
            var command = await Command(provider);
            using (var invalid = provider.CreateScope())
            {
                var result = await invalid.ServiceProvider.GetRequiredService<IMediator>().Send(command with { Language = "xx" });
                Assert.True(result.IsFailure);
            }
            using (var valid = provider.CreateScope())
            {
                var result = await valid.ServiceProvider.GetRequiredService<IMediator>().Send(command);
                Assert.True(result.IsSuccess, result.Error?.Message);
            }
            resolver.Verify(x => x.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            var audits = await db.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.Single(audits.Where(x => x.Success));
            Assert.Single(audits.Where(x => !x.Success));
            Assert.All(audits, x => { Assert.Equal(TestTenants.Second, x.TenantId); Assert.Equal(OrderId, x.ResourceId); Assert.Null(x.UserId); });
        }, transactional: false);
    }

    [Fact]
    public async Task Failed_refund_never_claims_a_refund_amount_in_response_or_email()
    {
        var run = new Run();
        await TestMethod<bool>(setup: (IServiceCollection services) =>
        {
            run.Setup(services);
            run.Stripe.Setup(x => x.RefundPaymentIntentAsync("pi_guest", It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Stripe.StripeException("recording Stripe failure"));
            return Task.CompletedTask;
        }, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            var result = await provider.GetRequiredService<IMediator>().Send(await Command(provider));
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.False(result.Value.RefundInitiated);
            Assert.Null(result.Value.ActualRefundAmount);
            var body = await provider.GetRequiredService<CleansiaDbContext>().OutboxMessages.IgnoreQueryFilters()
                .Where(x => x.QueueName == QueueNames.SendEmail).Select(x => x.Body).SingleAsync();
            using var send = provider.CreateScope();
            await ActivatorUtilities.CreateInstance<SendEmailHandler>(send.ServiceProvider).HandleAsync(body, CancellationToken.None);
            Assert.Equal(1, run.Deliveries);
            Assert.Null(run.DeliveredAmount);
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.IgnoreQueryFilters().SingleAsync()).CurrentStatus);
            Assert.Equal(RefundStatus.Pending, (await db.Refunds.IgnoreQueryFilters().SingleAsync()).Status);
        }, transactional: false);
    }

    [Fact]
    public async Task Erased_guest_destination_is_not_reintroduced_by_pending_cancellation_email()
    {
        var run = new Run();
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db, paid: false), act: async (IServiceProvider provider) =>
        {
            var result = await provider.GetRequiredService<IMediator>().Send(await Command(provider));
            Assert.True(result.IsSuccess, result.Error?.Message);
            string body;
            using (var erase = provider.CreateScope())
            {
                erase.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                var db = erase.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                body = await db.OutboxMessages.Where(x => x.QueueName == QueueNames.SendEmail).Select(x => x.Body).SingleAsync();
                (await db.Orders.SingleAsync()).AnonymizeCustomerData();
                await db.CommitAsync(CancellationToken.None);
            }
            using var send = provider.CreateScope();
            await ActivatorUtilities.CreateInstance<SendEmailHandler>(send.ServiceProvider).HandleAsync(body, CancellationToken.None);
            Assert.Equal(0, run.Deliveries);
            Assert.DoesNotContain(Email, body);
            return true;
        }, transactional: false);
    }

    [Fact]
    public async Task Final_commit_failure_keeps_refund_durable_and_fresh_retry_finishes_cancel_audit_and_email_once()
    {
        var run = new Run { FailFinalCommit = true };
        await TestMethod<bool>(setup: run.Setup, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            var command = await Command(provider);
            using (var attempt = provider.CreateScope())
                await Assert.ThrowsAsync<DbUpdateException>(() => attempt.ServiceProvider.GetRequiredService<IMediator>().Send(command));
            using (var read = provider.CreateScope())
            {
                var db = read.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                await AssertUncancelled(db, OrderStatus.Confirmed);
                Assert.Equal(RefundStatus.Succeeded, (await db.Refunds.IgnoreQueryFilters().SingleAsync()).Status);
                Assert.Equal(PaymentStatus.Refunded, (await db.Orders.IgnoreQueryFilters().SingleAsync()).PaymentStatus);
            }
            using (var retry = provider.CreateScope())
            {
                var result = await retry.ServiceProvider.GetRequiredService<IMediator>().Send(command);
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(1000m, result.Value.ActualRefundAmount);
            }
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            Assert.Equal(OrderStatus.Cancelled, (await db.Orders.IgnoreQueryFilters().SingleAsync()).CurrentStatus);
            Assert.Single(await db.CustomerActionAudits.IgnoreQueryFilters().Where(x => x.Success).ToListAsync());
            Assert.Single(await db.OutboxMessages.IgnoreQueryFilters().Where(x => x.QueueName == QueueNames.SendEmail).ToListAsync());
            Assert.Single(await db.Refunds.IgnoreQueryFilters().ToListAsync());
            run.Stripe.Verify(x => x.RefundPaymentIntentAsync("pi_guest", 1000m, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }, transactional: false);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cleaner_start_during_refund_or_before_final_commit_cannot_be_overwritten(bool duringStripe)
    {
        var run = new Run();
        Func<Task>? beforeFinalCommit = null;
        await TestMethod<bool>(setup: (IServiceCollection services) =>
        {
            run.Setup(services);
            var loyalty = new Mock<ILoyaltyService>();
            loyalty.Setup(x => x.RevokeForCancelledOrderAsync(OrderId, It.IsAny<CancellationToken>()))
                .Returns(async () => { if (beforeFinalCommit is not null) await beforeFinalCommit(); });
            services.Replace(ServiceDescriptor.Scoped<ILoyaltyService>(_ => loyalty.Object));
            return Task.CompletedTask;
        }, arrange: (CleansiaDbContext db) => Seed(db), act: async (IServiceProvider provider) =>
        {
            var command = await Command(provider);
            if (duringStripe) run.DuringStripe = () => StartInSeparateScope(provider);
            else beforeFinalCommit = () => StartInSeparateScope(provider);
            using (var attempt = provider.CreateScope())
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => attempt.ServiceProvider.GetRequiredService<IMediator>().Send(command));
            using (var read = provider.CreateScope())
            {
                var db = read.ServiceProvider.GetRequiredService<CleansiaDbContext>();
                await AssertUncancelled(db, OrderStatus.InProgress);
                var refund = await db.Refunds.IgnoreQueryFilters().SingleAsync();
                Assert.Equal(duringStripe ? RefundStatus.Pending : RefundStatus.Succeeded, refund.Status);
                Assert.Equal(1000m, refund.Amount);
            }
            run.DuringStripe = null;
            using (var recovery = provider.CreateScope())
            {
                recovery.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(TestTenants.Second);
                var refund = await recovery.ServiceProvider.GetRequiredService<IRefundService>().IssueRefundAsync(
                    new RefundRequest(OrderId, 1000m, RefundReason.CustomerCancellation, "System"), CancellationToken.None);
                Assert.True(refund.IsSuccess, refund.Error?.Message);
            }
            using (var retry = provider.CreateScope())
            {
                var refusal = await retry.ServiceProvider.GetRequiredService<IMediator>().Send(command);
                Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, refusal.Error?.Message);
            }
            return true;
        }, assert: async (CleansiaDbContext db, bool _) =>
        {
            await AssertUncancelled(db, OrderStatus.InProgress);
            Assert.Equal(RefundStatus.Succeeded, (await db.Refunds.IgnoreQueryFilters().SingleAsync()).Status);
            Assert.Single(run.StripeRefunds);
        }, transactional: false);
    }
}
