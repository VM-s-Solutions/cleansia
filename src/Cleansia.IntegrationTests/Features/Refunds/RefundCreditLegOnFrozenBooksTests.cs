using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

/// <summary>
/// The customer belongs to a company frozen for archive and booked with another company: 2000, of which 500
/// was paid in credit. Their complaint asks for credit, which their sealed books cannot take, so its 400
/// settlement goes to the card through the refund seam. The seam's credit leg would land on the same sealed
/// account by raw SQL, past the frozen-books guard; it is skipped instead, as that company's credit is
/// written off when it closes. The card share still goes back and the resolution commits. A leg skipped
/// this way leaves no ledger row, so an order that ends later would find that credit still owed; that
/// return is skipped the same way.
/// </summary>
[Collection("PostgresCollection")]
public class RefundCreditLegOnFrozenBooksTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-frozen-refund";
    private const string CzkId = "cur-czk-frozen-refund";
    private const string OrderId = "order-frozen-refund";
    private const string PaymentIntentId = "pi_frozen_refund";

    private readonly Mock<IStripeClient> _stripe = new();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("admin-frozen-refund", "admin@cleansia.test"),
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

    private async Task<(string UserId, string DisputeId)> SeedAsync(OrderStatus status = OrderStatus.Completed)
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
            var user = User.CreateWithPassword("frozen-refund@cleansia.test", "Seed-Password-123", "Frozen", "Refund");
            user.TenantId = TestTenants.Second;
            ctx.Users.Add(user);
            await ctx.CommitAsync(CancellationToken.None);
            userId = user.Id;
        }

        string disputeId;
        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(userId, CzkId, CancellationToken.None);
            account!.Issue(500m, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");

            var order = Order.Create(
                customerName: "Frozen Refund",
                customerEmail: "frozen-refund@cleansia.test",
                customerPhone: "+420000000003",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(-1),
                paymentType: PaymentType.Card,
                totalPrice: 2000m,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Paid,
                userId: userId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.ApplyCredit(500m, userId);
            order.AssignStripePaymentIntentId(PaymentIntentId);
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
            ctx.Orders.Add(order);
            var dispute = new Dispute(
                OrderId, userId, DisputeReason.QualityIssue, "The bathroom was not cleaned.", userId,
                DisputeSettlementPreference.Credit);
            ctx.Disputes.Add(dispute);
            await ctx.CommitAsync(CancellationToken.None);
            disputeId = dispute.Id;

            Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                account.Id, 500m, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", userId,
                CancellationToken.None, orderId: OrderId));
        }

        await using (var ctx = NewContext())
        {
            var company = await ctx.Tenants.SingleAsync(t => t.Id == TestTenants.Second);
            var frozenOn = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
            company.RequestWindDown(new DateOnly(2026, 9, 1), "admin-frozen-refund", frozenOn.AddDays(-30));
            company.Deactivate("admin-frozen-refund", frozenOn.AddDays(-15));
            company.RequestArchive("admin-frozen-refund", frozenOn);
            await ctx.CommitAsync(CancellationToken.None);
        }

        return (userId, disputeId);
    }

    [Fact]
    public async Task A_Complaint_Settled_To_The_Card_Returns_No_Credit_Onto_The_Frozen_Companys_Books()
    {
        await ResetAsync();
        var (userId, disputeId) = await SeedAsync();
        var refundKey = $"refund:{OrderId}:dispute:{disputeId}";

        await using (var ctx = NewContext())
        {
            var factory = new Mock<IStripeClientFactory>();
            factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
            var handler = new ResolveDispute.Handler(
                new DisputeRepository(ctx),
                new TestUserSessionProvider("admin-frozen-refund", "admin@cleansia.test"),
                new RefundService(
                    new RefundRepository(ctx),
                    new OrderRepository(ctx),
                    new CreditAccountRepository(ctx),
                    factory.Object,
                    Mock.Of<IAdminNotifier>(),
                    new UserNotificationRepository(ctx),
                    NullLogger<RefundService>.Instance),
                new RefundRepository(ctx),
                new CreditAccountRepository(ctx),
                new OrderEmployeePayRepository(ctx),
                Mock.Of<ILoyaltyService>(),
                Mock.Of<INotificationProducer>(),
                new AuditContext());

            var result = await handler.Handle(
                new ResolveDispute.Command(disputeId, 400m, "Settled for the missed bathroom."), CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);

            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 300m, refundKey, It.IsAny<CancellationToken>()), Times.Once);
        await using var verify = NewContext();
        var account = await verify.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        Assert.Equal(TestTenants.Second, account.TenantId);
        Assert.DoesNotContain(account.Transactions, t => t.Reason == CreditTransactionReason.OrderPaymentReturned);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        var dispute = await verify.Disputes.IgnoreQueryFilters().AsNoTracking().SingleAsync(d => d.Id == disputeId);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
        Assert.Equal(300m, dispute.CardRefundedAmount);
        Assert.Equal(0m, dispute.CreditReturnedAmount);
    }

    /// <summary>
    /// An administrator's full refund of the 2000 order, still to be cleaned, refunds the 1500 card and skips
    /// the 500 credit leg; the order then reads <c>Refunded</c> with none of its credit returned. Cancelling it
    /// afterwards takes the return for an order that ended with no card refund of its own, which would find
    /// those 500 still owed and write them onto the sealed account. It writes nothing, and the cancel commits.
    /// </summary>
    [Fact]
    public async Task A_Credit_Leg_Skipped_On_Frozen_Books_Is_Not_Written_There_When_The_Order_Is_Cancelled_Later()
    {
        await ResetAsync();
        var (userId, _) = await SeedAsync(OrderStatus.New);
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);

        await using (var ctx = NewContext())
        {
            var refund = await new RefundService(
                    new RefundRepository(ctx),
                    new OrderRepository(ctx),
                    new CreditAccountRepository(ctx),
                    factory.Object,
                    Mock.Of<IAdminNotifier>(),
                    new UserNotificationRepository(ctx),
                    NullLogger<RefundService>.Instance)
                .IssueRefundAsync(
                    new RefundRequest(OrderId, 2000m, RefundReason.AdminDiscretion, "admin-frozen-refund", RefundRequestId: "full"),
                    CancellationToken.None);

            Assert.True(refund.IsSuccess, refund.Error?.Message);
            Assert.Equal(1500m, refund.Value!.Amount);
            Assert.Equal(0m, refund.Value.CreditReturned);
        }

        await using (var ctx = NewContext())
        {
            var order = await new OrderRepository(ctx).GetByIdAsync(OrderId, CancellationToken.None);
            Assert.Equal(PaymentStatus.Refunded, order!.PaymentStatus);
            var refunds = new RefundRepository(ctx);
            await new PlatformOrderCancellation(
                    new RefundService(
                        refunds, new OrderRepository(ctx), new CreditAccountRepository(ctx), factory.Object,
                        Mock.Of<IAdminNotifier>(),
                        new UserNotificationRepository(ctx),
                        NullLogger<RefundService>.Instance),
                    refunds,
                    new CreditAccountRepository(ctx),
                    Mock.Of<ILoyaltyService>(),
                    Mock.Of<INotificationProducer>(),
                    Mock.Of<ILiveActivityProducer>(),
                    Mock.Of<IExpressWaiverConsumer>(),
                    new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
                    Mock.Of<IPendingDispatch>(),
                    NullLogger<PlatformOrderCancellation>.Instance)
                .CancelAsync(order, "admin-frozen-refund", CancelledBy.Admin, null, RefundReason.CustomerCancellation,
                    CancellationToken.None);
            await ctx.CommitAsync(CancellationToken.None);
        }

        _stripe.Verify(s => s.RefundPaymentIntentAsync(
            PaymentIntentId, 1500m, $"refund:{OrderId}:admin:full", It.IsAny<CancellationToken>()), Times.Once);
        _stripe.VerifyNoOtherCalls();
        await using var verify = NewContext();
        var account = await verify.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
            .Include(a => a.Transactions)
            .SingleAsync(a => a.UserId == userId && a.CurrencyId == CzkId);
        Assert.DoesNotContain(account.Transactions, t => t.Reason == CreditTransactionReason.OrderPaymentReturned);
        Assert.Equal(0m, account.Balance);
        Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));
        var cancelled = await verify.Orders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == OrderId);
        Assert.Equal(OrderStatus.Cancelled, cancelled.CurrentStatus);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
