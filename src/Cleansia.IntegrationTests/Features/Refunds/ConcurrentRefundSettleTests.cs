using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Refunds;

/// <summary>
/// Two refunds of one paid order settle at the same moment, on two connections to real Postgres. The payment
/// status a settle writes is derived from what the order has given back, so the settle that commits second,
/// refused because the other moved that status, reads the refunds again and saves the status the two make
/// together, instead of failing after Stripe has paid and leaving its refund pending, and with it everything a
/// cancellation does after its refund. A settle that carries a decision of its own on the order stays refused.
///
/// <para>The refusal comes from <c>PaymentStatus</c> being the order's second concurrency token (ADR-0071 D1).
/// Without that token neither settle is refused, and the later one saves a status counted without the other's
/// refund.</para>
/// </summary>
[Collection("PostgresCollection")]
public class ConcurrentRefundSettleTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-settle-race";
    private const string CzkId = "cur-czk-settle-race";
    private const string OrderId = "order-settle-race";
    private const string PaymentIntentId = "pi_settle_race";
    private const string AdminId = "admin-settle-race";
    private const string CleanerUserId = "cleaner-settle-race";
    private const string KeyA = $"refund:{OrderId}:admin:a";
    private const string KeyB = $"refund:{OrderId}:admin:b";
    private const string CancelKey = $"refund:{OrderId}:cancel";

    private readonly Mock<IStripeClient> _stripe = new();
    private readonly List<(decimal Amount, string Key)> _stripeRefunds = [];
    private readonly Mock<IExpressWaiverConsumer> _waiver = new();

    private CleansiaDbContext NewContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .AddInterceptors(interceptors)
            .Options,
        new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
        new FixedTenantProvider(TestTenants.Default));

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

        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, decimal, string, CancellationToken>((_, amount, key, _) => _stripeRefunds.Add((amount, key)))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// A guest's paid card booking of 1000 that a cleaner took, the link the guest was sent, and the card refunds
    /// already claimed on it whose Stripe answer has not come back, oldest first.
    /// </summary>
    private async Task SeedAsync(params (string Key, decimal Amount)[] pendingRefunds)
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "CZK", "CZK");
        currency.Id = CzkId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        ctx.Currencies.Add(currency);

        var order = Order.Create(
            customerName: "Settle Race",
            customerEmail: "settle-race@example.test",
            customerPhone: "+420000000005",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetMaxEmployees(1);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        var cleaner = User.CreateWithPassword("cleaner-settle-race@example.test", "Password123!", "Clean", "Er",
            UserProfile.Employee);
        cleaner.Id = CleanerUserId;
        cleaner.TenantId = TestTenants.Default;
        var employee = Employee.CreateWithUser(cleaner);
        employee.TenantId = TestTenants.Default;
        ctx.Employees.Add(employee);
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        ctx.Orders.Add(order);
        ctx.GuestOrderAccessTokens.Add(
            GuestOrderAccessToken.Issue(OrderId, GuestOrderAccessToken.ExpiryFor(order.CleaningDateTime)));

        for (var i = 0; i < pendingRefunds.Length; i++)
        {
            var (key, amount) = pendingRefunds[i];
            var claim = Refund.Create(OrderId, key, amount, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
            claim.Created("seed", DateTimeOffset.UtcNow.AddMinutes(i - pendingRefunds.Length));
            ctx.Refunds.Add(claim);
        }

        await ctx.CommitAsync(CancellationToken.None);
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
            Mock.Of<IAdminNotifier>(),
            new UserNotificationRepository(ctx),
            NullLogger<RefundService>.Instance);
    }

    private static RefundRequest AdminRefund(decimal amount, string requestId) =>
        new(OrderId, amount, RefundReason.AdminDiscretion, AdminId, RefundRequestId: requestId);

    /// <summary>The administrator's refund "b" of 400, run to completion on a connection of its own.</summary>
    private async Task SettleRefundBAsync()
    {
        await using var ctx = NewContext();
        var settled = await NewRefundService(ctx).IssueRefundAsync(AdminRefund(400m, "b"), CancellationToken.None);
        Assert.True(settled.IsSuccess, settled.Error?.Message);
    }

    /// <summary>An administrator cancels the booking, loaded as the admin cancel loads it.</summary>
    private async Task<PlatformOrderCancellationResult> CancelAsync(CleansiaDbContext ctx)
    {
        var order = await ctx.Orders
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
                .ThenInclude(a => a.Employee)
            .SingleAsync(o => o.Id == OrderId);
        var pending = new OutboxPendingDispatch(ctx);
        return await new PlatformOrderCancellation(
                NewRefundService(ctx),
                new RefundRepository(ctx),
                new CreditAccountRepository(ctx),
                Mock.Of<ILoyaltyService>(),
                new NotificationProducer(new UserNotificationRepository(ctx), pending, new UserRepository(ctx),
                    NullLogger<NotificationProducer>.Instance),
                Mock.Of<ILiveActivityProducer>(),
                _waiver.Object,
                new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
                pending,
                NullLogger<PlatformOrderCancellation>.Instance)
            .CancelAsync(order, AdminId, CancelledBy.Admin, "Double booking on our side",
                RefundReason.CustomerCancellation, CancellationToken.None);
    }

    private async Task<(Order Order, Dictionary<string, Refund> Refunds)> ReadAsync()
    {
        await using var ctx = NewContext();
        var order = await ctx.Orders.IgnoreQueryFilters().AsNoTracking().SingleAsync(o => o.Id == OrderId);
        var refunds = await ctx.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId)
            .ToDictionaryAsync(r => r.RefundKey);
        return (order, refunds);
    }

    [Fact]
    public async Task Two_Refunds_Settling_At_Once_Are_Both_Recorded_And_The_Order_Is_Refunded()
    {
        await ResetAsync();
        await SeedAsync();

        BusinessResult<RefundResult> refundA;
        await using (var ctx = NewContext(new AtTheSettleSave(SettleRefundBAsync)))
        {
            refundA = await NewRefundService(ctx).IssueRefundAsync(AdminRefund(600m, "a"), CancellationToken.None);
        }

        Assert.True(refundA.IsSuccess, refundA.Error?.Message);
        Assert.Equal(600m, refundA.Value!.Amount);
        var (order, refunds) = await ReadAsync();
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal((RefundStatus.Succeeded, 600m), (refunds[KeyA].Status, refunds[KeyA].Amount));
        Assert.Equal((RefundStatus.Succeeded, 400m), (refunds[KeyB].Status, refunds[KeyB].Amount));
        Assert.Equal(new List<(decimal, string)> { (600m, KeyA), (400m, KeyB) }, _stripeRefunds);
    }

    [Fact]
    public async Task A_Cancellation_Whose_Refund_Settles_Beside_Another_Still_Tells_The_Crew_And_The_Guest()
    {
        await ResetAsync();
        await SeedAsync((KeyB, 400m));

        await using (var ctx = NewContext(new AtTheSettleSave(SettleRefundBAsync)))
        {
            var cancellation = await CancelAsync(ctx);
            Assert.True(cancellation.Refund.Initiated, cancellation.Refund.FailureMessage);
            Assert.Equal(600m, cancellation.Refund.RefundedAmount);
            await ctx.CommitAsync(CancellationToken.None);
        }

        var (order, refunds) = await ReadAsync();
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Refunded, order.PaymentStatus);
        Assert.Equal((RefundStatus.Succeeded, 600m), (refunds[CancelKey].Status, refunds[CancelKey].Amount));
        Assert.Equal((RefundStatus.Succeeded, 400m), (refunds[KeyB].Status, refunds[KeyB].Amount));

        await using var read = NewContext();
        Assert.Single(await read.Set<UserNotification>().IgnoreQueryFilters()
            .Where(n => n.UserId == CleanerUserId && n.EventKey == NotificationEventCatalog.OrderAssignmentCancelled)
            .ToListAsync());
        var outbox = await read.OutboxMessages.IgnoreQueryFilters()
            .Select(m => new { m.QueueName, m.MessageKey })
            .ToListAsync();
        Assert.Contains(outbox, m => m.QueueName == QueueNames.NotificationsDispatch
            && m.MessageKey == MessageKeys.Push(CleanerUserId, NotificationEventCatalog.OrderAssignmentCancelled, OrderId));
        Assert.Contains(outbox, m => m.QueueName == QueueNames.SendEmail
            && m.MessageKey == MessageKeys.GuestOrderCancelledEmail(OrderId));
        Assert.All(await read.GuestOrderAccessTokens.IgnoreQueryFilters().Where(t => t.OrderId == OrderId).ToListAsync(),
            token => Assert.NotNull(token.RevokedOn));
        _waiver.Verify(w => w.ReleaseForOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Settle_That_Carries_A_Cancel_Staged_On_The_Order_Is_Refused_Rather_Than_Read_Again()
    {
        await ResetAsync();
        await SeedAsync((KeyB, 400m), (CancelKey, 600m));

        await using (var ctx = NewContext(new AtTheSettleSave(SettleRefundBAsync)))
        {
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => CancelAsync(ctx));
        }

        var (order, refunds) = await ReadAsync();
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Null(order.CancelledAt);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
        Assert.Equal(RefundStatus.Pending, refunds[CancelKey].Status);
        Assert.Equal(RefundStatus.Succeeded, refunds[KeyB].Status);
    }

    /// <summary>Runs another settle to completion once, at the save that records a refund as paid.</summary>
    private sealed class AtTheSettleSave(Func<Task> settleAnother) : SaveChangesInterceptor
    {
        private bool _ran;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_ran && eventData.Context!.ChangeTracker.Entries<Refund>()
                    .Any(e => e.State == EntityState.Modified && e.Entity.Status == RefundStatus.Succeeded))
            {
                _ran = true;
                await settleAnother();
            }

            return result;
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
