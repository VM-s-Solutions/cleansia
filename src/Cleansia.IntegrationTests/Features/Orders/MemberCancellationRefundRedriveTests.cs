using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;
using StripeException = Stripe.StripeException;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// A signed-in customer cancels a 2000 card order that took 500 of credit, while Stripe is down or
/// refusing — against real Postgres with the real refund seam. The cancel completes, the card refund
/// waits Pending on its key, the 500 of credit comes back at once on that same key, and the hourly
/// re-drive then refunds the card exactly once without returning the credit a second time.
/// </summary>
[Collection("PostgresCollection")]
public class MemberCancellationRefundRedriveTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-member-redrive";
    private const string CzkId = "cur-czk-member-redrive";
    private const string OrderId = "order-member-redrive";
    private const string PaymentIntentId = "pi_member_redrive";
    private const string RefundKey = $"refund:{OrderId}:cancel";
    private const decimal Total = 2000m;
    private const decimal Credit = 500m;
    private const decimal CardShare = 1500m;

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

    private async Task<string> SeedAsync()
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
            ctx.Currencies.Add(currency);
            var user = User.CreateWithPassword("member-redrive@cleansia.test", "Seed-Password-123", "Member", "Redrive");
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            account!.Issue(Credit, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");

            var order = Order.Create(
                customerName: "Member Redrive",
                customerEmail: "member-redrive@cleansia.test",
                customerPhone: "+420000000001",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(10),
                paymentType: PaymentType.Card,
                totalPrice: Total,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Paid,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.ApplyCredit(Credit, userId);
            order.AssignStripePaymentIntentId(PaymentIntentId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
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

    private NotificationProducer NewProducer(CleansiaDbContext ctx) =>
        new(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx),
            NullLogger<NotificationProducer>.Instance);

    private async Task<CancelOrder.Response> CancelAsync(string userId)
    {
        await using var ctx = NewContext();
        var cancellation = new CustomerOrderCancellation(
            new FixedTenantProvider(TestTenants.Default),
            NewRefundService(ctx),
            new RefundRepository(ctx),
            new ReceivableRepository(ctx),
            new CreditAccountRepository(ctx),
            Mock.Of<ILoyaltyService>(),
            new CancellationPolicyResolver(new UserMembershipRepository(ctx), new OrderRepository(ctx)),
            NewProducer(ctx),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            Mock.Of<IPendingDispatch>(),
            new AuditContext(),
            TimeProvider.System,
            NullLogger<CustomerOrderCancellation>.Instance);
        var order = await ctx.Orders
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .Include(o => o.Currency)
            .SingleAsync(o => o.Id == OrderId);

        var result = await cancellation.ExecuteAsync(order, reason: null, userId, CancellationToken.None);
        await ctx.CommitAsync(CancellationToken.None);
        return result.Response;
    }

    private async Task<RedrivePendingRefunds.Response> RedriveAsync()
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
            NewProducer(watchdogContext),
            Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(watchdogContext),
            new FixedTenantProvider(TestTenants.Default),
            watchdogContext,
            NullLogger<RedrivePendingRefunds.Handler>.Instance);
        var result = await watchdog.Handle(new RedrivePendingRefunds.Command(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    public static TheoryData<Exception> StripeFailures() =>
    [
        new HttpRequestException("connection reset"),
        new StripeException("card network unavailable"),
    ];

    [Theory]
    [MemberData(nameof(StripeFailures))]
    public async Task The_Cancel_Completes_The_Credit_Comes_Back_Now_And_The_Card_Once_On_The_Redrive(Exception failure)
    {
        await ResetAsync();
        var userId = await SeedAsync();
        _stripe.SetupSequence(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), RefundKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Returns(Task.CompletedTask);

        var response = await CancelAsync(userId);

        Assert.False(response.RefundInitiated);
        Assert.True(response.RefundPending);
        await using (var ctx = NewContext())
        {
            var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
            Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
            var refund = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync();
            Assert.Equal(RefundStatus.Pending, refund.Status);
            Assert.Equal(CardShare, refund.Amount);
            var returned = await ctx.CreditTransactions.AsNoTracking()
                .SingleAsync(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.OrderPaymentReturned);
            Assert.Equal(Credit, returned.Amount);
            Assert.Equal($"credit-return:{RefundKey}", returned.IdempotencyKey);
        }

        var redrive = await RedriveAsync();

        Assert.Equal(1, redrive.Redriven);
        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, CardShare, RefundKey, It.IsAny<CancellationToken>()), Times.Exactly(2));
        await using (var ctx = NewContext())
        {
            var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
            Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
            Assert.Equal(RefundStatus.Succeeded, (await ctx.Refunds.IgnoreQueryFilters().AsNoTracking().SingleAsync()).Status);
            var account = await ctx.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
                .Include(a => a.Transactions)
                .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
            Assert.Single(account.Transactions, t => t.Reason == CreditTransactionReason.OrderPaymentReturned);
            Assert.Equal(Credit, account.Balance);
            Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
