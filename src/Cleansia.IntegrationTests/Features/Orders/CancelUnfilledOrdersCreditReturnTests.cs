using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
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
        PaymentType paymentType = PaymentType.Card, PaymentStatus paymentStatus = PaymentStatus.Paid)
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
                userId: userId);
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
            new CreditAccountRepository(ctx),
            NewRefundService(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), NullLogger<NotificationProducer>.Instance),
            new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
            new OutboxPendingDispatch(ctx),
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

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
