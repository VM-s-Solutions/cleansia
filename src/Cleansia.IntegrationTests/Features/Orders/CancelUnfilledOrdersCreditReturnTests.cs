using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Polly.Timeout;
using Respawn;
using StripeException = Stripe.StripeException;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// An order that took 500 of credit and that nobody took, swept against real Postgres with the real
/// refund service: the customer gets the 500 back on their balance exactly once. On a card order that
/// holds whether the 1500 card refund goes through on the sweep, fails at Stripe, or never reaches it;
/// on an unpaid cash order it holds with no refund at all.
/// </summary>
[Collection("PostgresCollection")]
public class CancelUnfilledOrdersCreditReturnTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-unfilled-credit";
    private const string CzkId = "cur-czk-unfilled-credit";
    private const string OrderId = "order-unfilled-credit";
    private const string PaymentIntentId = "pi_unfilled_credit";
    private const string RefundKey = $"refund:{OrderId}:admin";
    private const decimal Total = 2000m;
    private const decimal Credit = 500m;
    private const decimal CardShare = 1500m;
    private const decimal Apology = 250m;

    private readonly Mock<IStripeClient> _stripe = new();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
    }

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
        await SeedTenantRegistryAsync(conn);
    }

    /// <summary>
    /// The state checkout leaves behind: a 500 grant spent on the order under its payment key, so the
    /// balance is zero and still equals the ledger, and an order with nobody on it — by default a paid
    /// card order.
    /// </summary>
    private async Task<string> SeedAsync(
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        decimal settledInCredit = 0m)
    {
        string userId;
        await using (var ctx = NewContext())
        {
            ctx.Languages.Add(Language.Create("en", "English"));
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            country.Id = CountryId;
            ctx.Countries.Add(country);
            var currency = Currency.Create("CZK", "CZK", "CZK");
            currency.Id = CzkId;
            currency.IsActive = true;
            currency.SetAsDefault(true);
            currency.SetNoShowCredit(Apology);
            ctx.Currencies.Add(currency);
            var user = User.CreateWithPassword("unfilled-credit@cleansia.test", "Seed-Password-123", "Unfilled", "Credit");
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            account!.Issue(Credit, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");
            if (settledInCredit > 0m)
            {
                account.Issue(
                    settledInCredit, CreditTransactionReason.DisputeSettlement, "dispute-settlement:dispute-earlier",
                    "admin", orderId: OrderId, disputeId: "dispute-earlier");
            }

            var order = Order.Create(
                customerName: "Unfilled Credit",
                customerEmail: "unfilled-credit@cleansia.test",
                customerPhone: "+420000000000",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddHours(-2),
                paymentType: paymentType,
                totalPrice: Total,
                currencyId: CzkId,
                paymentStatus: paymentStatus,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.ApplyCredit(Credit, userId);
            if (paymentType == PaymentType.Card)
            {
                order.AssignStripePaymentIntentId(PaymentIntentId);
            }
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
            ctx.Orders.Add(order);
            await ctx.CommitAsync(CancellationToken.None);

            Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                account.Id, Credit, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", userId,
                CancellationToken.None, orderId: OrderId));
        }

        return userId;
    }

    private RefundService NewRefundService(CleansiaDbContext ctx)
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        return new RefundService(
            new RefundRepository(ctx),
            new OrderRepository(ctx),
            new CreditAccountRepository(ctx),
            factory.Object,
            NullLogger<RefundService>.Instance);
    }

    private async Task<CancelUnfilledOrders.Response> SweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new CancelUnfilledOrders.Handler(
            new OrderRepository(ctx),
            new CleanerNoShowCancellation(
                new CreditAccountRepository(ctx),
                NewRefundService(ctx),
                new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), NullLogger<NotificationProducer>.Instance),
                new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
                new OutboxPendingDispatch(ctx),
                NullLogger<CleanerNoShowCancellation>.Instance),
            new FixedTenantProvider(TestTenants.Default),
            ctx,
            NullLogger<CancelUnfilledOrders.Handler>.Instance);

        var result = await handler.Handle(new CancelUnfilledOrders.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task<(List<CreditTransaction> Returns, CreditAccount Account, Refund? Refund, Order Order)> ReadAsync(string userId)
    {
        await using var ctx = NewContext();
        var returns = await ctx.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.OrderPaymentReturned)
            .ToListAsync();
        var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync();
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == OrderId);
        return (returns, account, refund, order);
    }

    [Fact]
    public async Task A_Refunded_Card_Order_Returns_Its_Applied_Credit_Exactly_Once()
    {
        await ResetAsync();
        var userId = await SeedAsync();

        var response = await SweepAsync();

        Assert.Equal(1, response.CancelledCount);
        Assert.Equal(1, response.RefundedCount);
        Assert.Equal(1, response.CreditedCount);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, CardShare, RefundKey, It.IsAny<CancellationToken>()), Times.Once);

        var (returns, account, refund, order) = await ReadAsync(userId);
        var returned = Assert.Single(returns);
        Assert.Equal(Credit, returned.Amount);
        Assert.Equal($"credit-return:{RefundKey}", returned.IdempotencyKey);
        Assert.DoesNotContain(account.Transactions, t => t.IdempotencyKey == $"credit-return:order-ended-unpaid:{OrderId}");
        Assert.Single(account.Transactions, t => t.IdempotencyKey == $"cleaner-noshow:{OrderId}");
        Assert.Equal(Credit + Apology, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(RefundStatus.Succeeded, refund!.Status);
        Assert.Equal(CardShare, refund.Amount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    [Fact]
    public async Task A_Failed_Card_Refund_Returns_The_Credit_Once_And_A_Later_Redrive_Refunds_Only_The_Card()
    {
        await ResetAsync();
        var userId = await SeedAsync();
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("card network unavailable"))
            .Returns(Task.CompletedTask);

        var response = await SweepAsync();

        Assert.Equal(1, response.CancelledCount);
        Assert.Equal(0, response.RefundedCount);
        var (returns, account, refund, order) = await ReadAsync(userId);
        var returned = Assert.Single(returns);
        Assert.Equal(Credit, returned.Amount);
        Assert.Equal($"credit-return:order-ended-unpaid:{OrderId}", returned.IdempotencyKey);
        Assert.Equal(Credit + Apology, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(RefundStatus.Pending, refund!.Status);
        Assert.Equal(CardShare, refund.Amount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);

        await using (var ctx = NewContext())
        {
            var redrive = await NewRefundService(ctx).IssueRefundAsync(
                new RefundRequest(OrderId, Total, RefundReason.ServiceNotRendered, "system"), CancellationToken.None);
            Assert.True(redrive.IsSuccess, redrive.Error?.Message);
            Assert.Equal(CardShare, redrive.Value!.Amount);
        }

        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, CardShare, RefundKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        (returns, account, refund, order) = await ReadAsync(userId);
        Assert.Equal(Credit, Assert.Single(returns).Amount);
        Assert.Equal(Credit + Apology, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(RefundStatus.Succeeded, refund!.Status);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
    }

    /// <summary>
    /// A timeout, a dropped connection or an open circuit escapes RefundService after its claim commit
    /// has saved the cancel, so the order is already out of every later tick. The credit comes back on
    /// this one, once, and the card refund waits Pending on its deterministic key.
    /// </summary>
    [Theory]
    [MemberData(nameof(StripeTransportFailures))]
    public async Task A_Stripe_Transport_Fault_Still_Returns_The_Credit_Exactly_Once(Exception failure)
    {
        await ResetAsync();
        var userId = await SeedAsync();
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var response = await SweepAsync();

        Assert.Equal(1, response.CancelledCount);
        Assert.Equal(0, response.RefundedCount);
        Assert.Equal(1, response.CreditedCount);
        var (returns, account, refund, order) = await ReadAsync(userId);
        var returned = Assert.Single(returns);
        Assert.Equal(Credit, returned.Amount);
        Assert.Equal($"credit-return:order-ended-unpaid:{OrderId}", returned.IdempotencyKey);
        Assert.Single(account.Transactions, t => t.IdempotencyKey == $"cleaner-noshow:{OrderId}");
        Assert.Equal(Credit + Apology, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(RefundStatus.Pending, refund!.Status);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    public static TheoryData<Exception> StripeTransportFailures() =>
    [
        new HttpRequestException("connection reset"),
        new TimeoutRejectedException("total request timeout"),
    ];

    [Fact]
    public async Task An_Unpaid_Cash_Order_Returns_Its_Applied_Credit_Exactly_Once_Without_A_Refund()
    {
        await ResetAsync();
        var userId = await SeedAsync(PaymentType.Cash, PaymentStatus.Pending);

        var response = await SweepAsync();

        Assert.Equal(1, response.CancelledCount);
        Assert.Equal(0, response.RefundedCount);
        Assert.Equal(1, response.CreditedCount);
        _stripe.VerifyNoOtherCalls();
        var (returns, account, refund, order) = await ReadAsync(userId);
        var returned = Assert.Single(returns);
        Assert.Equal(Credit, returned.Amount);
        Assert.Equal($"credit-return:order-ended-unpaid:{OrderId}", returned.IdempotencyKey);
        Assert.Null(refund);
        Assert.Equal(Credit + Apology, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
    }

    /// <summary>
    /// The hourly watchdog re-drives the refund the sweep left pending, on its own key: the card comes
    /// back once and the credit, already returned by the sweep, is not returned a second time.
    /// </summary>
    [Fact]
    public async Task The_Hourly_Redrive_Refunds_The_Card_A_Failed_Sweep_Left_Pending_Once()
    {
        await ResetAsync();
        var userId = await SeedAsync();
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("card network unavailable"))
            .Returns(Task.CompletedTask);
        await SweepAsync();
        await using (var ctx = NewContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"Refunds\" SET \"CreatedOn\" = NOW() - INTERVAL '2 hours'");
        }

        RedrivePendingRefunds.Response redriven;
        await using (var ctx = NewContext())
        {
            var watchdog = new RedrivePendingRefunds.Handler(
                new RefundRepository(ctx),
                NewRefundService(ctx),
                new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), NullLogger<NotificationProducer>.Instance),
                Mock.Of<IAdminNotifier>(),
                new UserNotificationRepository(ctx),
                new FixedTenantProvider(TestTenants.Default),
                ctx,
                NullLogger<RedrivePendingRefunds.Handler>.Instance);
            var result = await watchdog.Handle(new RedrivePendingRefunds.Command(), CancellationToken.None);
            Assert.True(result.IsSuccess, result.Error?.Message);
            redriven = result.Value!;
        }

        Assert.Equal(1, redriven.Redriven);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, CardShare, RefundKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        var (returns, account, refund, order) = await ReadAsync(userId);
        Assert.Equal(Credit, Assert.Single(returns).Amount);
        Assert.Equal(Credit + Apology, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(RefundStatus.Succeeded, refund!.Status);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        await using (var ctx = NewContext())
        {
            Assert.True(await ctx.Set<UserNotification>().IgnoreQueryFilters()
                .AnyAsync(n => n.UserId == userId && n.EventKey == NotificationEventCatalog.OrderRefunded));
        }
    }

    /// <summary>
    /// One customer, three unfilled orders, one tick. Two carry no charge surface, so their applied
    /// credit comes back by a raw statement after an earlier order's apology was written through the
    /// tracked account — whatever order the tick meets them in. Each order commits on its own, so no
    /// tracked balance writes back over a raw return and the balance still equals its ledger.
    /// </summary>
    [Fact]
    public async Task Several_Orders_Of_One_Customer_In_One_Tick_Keep_Every_Credit_Movement()
    {
        await ResetAsync();
        var userId = await SeedAsync();
        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            account!.Issue(2 * Credit, CreditTransactionReason.Goodwill, "seed-grant-more", "seed", note: "n");
            foreach (var orderId in new[] { "order-unfilled-credit-b", "order-unfilled-credit-c" })
            {
                var order = Order.Create(
                    customerName: "Unfilled Credit",
                    customerEmail: "unfilled-credit@cleansia.test",
                    customerPhone: "+420000000000",
                    customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                    rooms: 1,
                    bathrooms: 1,
                    cleaningDateTime: DateTime.UtcNow.AddHours(-2),
                    paymentType: PaymentType.Card,
                    totalPrice: Total,
                    currencyId: CzkId,
                    paymentStatus: PaymentStatus.Paid,
                    userId: userId,
                    cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
                order.Id = orderId;
                order.SetMaxEmployees(1);
                order.ApplyCredit(Credit, userId);
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
                ctx.Orders.Add(order);
            }

            await ctx.CommitAsync(CancellationToken.None);
            foreach (var orderId in new[] { "order-unfilled-credit-b", "order-unfilled-credit-c" })
            {
                Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                    account.Id, Credit, CreditTransactionReason.OrderPayment, $"order-payment-{orderId}", userId,
                    CancellationToken.None, orderId: orderId));
            }
        }

        var response = await SweepAsync();

        Assert.Equal(3, response.CancelledCount);
        Assert.Equal(3, response.CreditedCount);
        await using var read = NewContext();
        var balance = await read.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        Assert.Equal(balance.Transactions.Sum(t => t.Amount), balance.Balance);
        Assert.Equal(3 * (Credit + Apology), balance.Balance);
    }

    private async Task<RedrivePendingRefunds.Response> WatchdogAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"Refunds\" SET \"CreatedOn\" = NOW() - INTERVAL '2 hours'");
        }

        await using var watchdogContext = NewContext();
        var watchdog = new RedrivePendingRefunds.Handler(
            new RefundRepository(watchdogContext),
            NewRefundService(watchdogContext),
            new NotificationProducer(new UserNotificationRepository(watchdogContext), new OutboxPendingDispatch(watchdogContext), new UserRepository(watchdogContext), NullLogger<NotificationProducer>.Instance),
            Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(watchdogContext),
            new FixedTenantProvider(TestTenants.Default),
            watchdogContext,
            NullLogger<RedrivePendingRefunds.Handler>.Instance);
        var result = await watchdog.Handle(new RedrivePendingRefunds.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task<decimal> GivenBackAsync()
    {
        await using var ctx = NewContext();
        var card = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId && r.Status == RefundStatus.Succeeded)
            .SumAsync(r => r.Amount);
        var credit = await ctx.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId
                && (t.Reason == CreditTransactionReason.OrderPaymentReturned
                    || t.Reason == CreditTransactionReason.DisputeSettlement))
            .SumAsync(t => t.Amount);
        return card + credit;
    }

    /// <summary>
    /// A complaint on the order was already settled in 200 of credit when nobody turned up. Stripe refuses
    /// the sweep's refund, so the applied credit comes back now net of the settlement, 300 of the 500; the
    /// hourly re-drive then holds the card to what the sale has left. 2000 goes back in all, never more.
    /// </summary>
    [Fact]
    public async Task A_No_Show_After_A_Complaint_Settled_In_Credit_Returns_No_More_Than_The_Price()
    {
        await ResetAsync();
        var userId = await SeedAsync(settledInCredit: 200m);
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("card network unavailable"))
            .Returns(Task.CompletedTask);

        await SweepAsync();

        var (returns, _, refund, _) = await ReadAsync(userId);
        var returned = Assert.Single(returns);
        Assert.Equal(300m, returned.Amount);
        Assert.Equal($"credit-return:order-ended-unpaid:{OrderId}", returned.IdempotencyKey);
        Assert.Equal(RefundStatus.Pending, refund!.Status);

        var redriven = await WatchdogAsync();

        Assert.Equal(1, redriven.Redriven);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 1300m, RefundKey, It.IsAny<CancellationToken>()), Times.Once);
        (returns, var account, refund, _) = await ReadAsync(userId);
        Assert.Equal(RefundStatus.Succeeded, refund!.Status);
        Assert.Equal(1300m, refund.Amount);
        Assert.Equal(500m, returns.Sum(t => t.Amount));
        Assert.Equal(Total, await GivenBackAsync());
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
    }

    [Fact]
    public async Task A_No_Show_Whose_Whole_Price_Was_Settled_In_Credit_Returns_Nothing_More()
    {
        await ResetAsync();
        var userId = await SeedAsync(settledInCredit: Total);

        var response = await SweepAsync();
        var redriven = await WatchdogAsync();

        Assert.Equal(1, response.CancelledCount);
        Assert.Equal(0, response.RefundedCount);
        Assert.Equal(0, redriven.Redriven);
        _stripe.VerifyNoOtherCalls();
        var (returns, account, refund, order) = await ReadAsync(userId);
        Assert.Null(refund);
        Assert.Empty(returns);
        Assert.Equal(Total, await GivenBackAsync());
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
