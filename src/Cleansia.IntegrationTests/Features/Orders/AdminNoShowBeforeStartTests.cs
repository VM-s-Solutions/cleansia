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
using Respawn;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// An administrator calls off, three hours before its start, a paid booking nobody has taken. Against real
/// Postgres with the real refund seam, the cancellation pays what the unfilled sweep would pay at the start —
/// the whole refund on the sweep's key and one apology credit — and once the slot has passed the sweep finds
/// nothing left to cancel or credit.
/// </summary>
[Collection("PostgresCollection")]
public class AdminNoShowBeforeStartTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-no-show-early";
    private const string CountryId = "country-cz-no-show-early";
    private const string CzkId = "cur-czk-no-show-early";
    private const string OrderId = "order-no-show-early";
    private const string PaymentIntentId = "pi_no_show_early";

    private readonly Mock<IStripeClient> _stripe = new();

    [Fact]
    public async Task An_Early_No_Show_On_An_Unstaffed_Paid_Booking_Pays_Once_And_Leaves_The_Sweep_Nothing()
    {
        await ResetAsync();
        await SeedAsync();
        _stripe.Setup(s => s.RefundPaymentIntentAsync(
                PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await using (var ctx = NewContext())
        {
            var confirmed = await NoShowHandler(ctx).Handle(new AdminCancelOrderAsNoShow.Command(OrderId), CancellationToken.None);
            Assert.True(confirmed.IsSuccess, confirmed.Error?.Message);
            Assert.Equal(1000m, confirmed.Value!.RefundedAmount);
            Assert.Equal(250m, confirmed.Value.ApologyCredit);
            await ctx.CommitAsync(CancellationToken.None);
        }

        await using (var rewind = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await rewind.OpenAsync();
            await using var command = new NpgsqlCommand(
                """UPDATE "Orders" SET "CleaningDateTime" = @slot WHERE "Id" = @id""", rewind);
            command.Parameters.AddWithValue("slot", DateTime.UtcNow.AddHours(-1));
            command.Parameters.AddWithValue("id", OrderId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var swept = await SweepAsync();

        Assert.Equal(0, swept.CancelledCount);
        await using var read = NewContext();
        var order = await read.Orders.IgnoreQueryFilters().AsNoTracking()
            .Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == OrderId);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CancelledBy.Admin, order.CancelledBy);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Single(order.OrderStatusHistory, s => s.Status == OrderStatus.Cancelled);
        var refund = Assert.Single(await read.Refunds.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.OrderId == OrderId).ToListAsync());
        Assert.Equal($"refund:{OrderId}:admin", refund.RefundKey);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        var apology = Assert.Single(await read.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.CleanerNoShow).ToListAsync());
        Assert.Equal($"cleaner-noshow:{OrderId}", apology.IdempotencyKey);
        Assert.Equal(250m, apology.Amount);
    }

    private AdminCancelOrderAsNoShow.Handler NoShowHandler(CleansiaDbContext ctx)
    {
        var refunds = new RefundRepository(ctx);
        return new AdminCancelOrderAsNoShow.Handler(
            new OrderRepository(ctx),
            new DisputeRepository(ctx),
            refunds,
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"),
            NoShowBody(ctx),
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IExpressWaiverConsumer>(),
            Mock.Of<ILoyaltyService>(),
            TimeProvider.System);
    }

    private async Task<CancelUnfilledOrders.Response> SweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new CancelUnfilledOrders.Handler(
            new OrderRepository(ctx),
            NoShowBody(ctx),
            new FixedTenantProvider(TestTenants.Default),
            ctx,
            NullLogger<CancelUnfilledOrders.Handler>.Instance);
        var result = await handler.Handle(new CancelUnfilledOrders.Command(GraceMinutes: 0), CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private CleanerNoShowCancellation NoShowBody(CleansiaDbContext ctx)
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        var refunds = new RefundRepository(ctx);
        return new CleanerNoShowCancellation(
            new CreditAccountRepository(ctx),
            new RefundService(
                refunds,
                new OrderRepository(ctx),
                new CreditAccountRepository(ctx),
                factory.Object,
                Mock.Of<IAdminNotifier>(),
                new UserNotificationRepository(ctx),
                NullLogger<RefundService>.Instance),
            refunds,
            Mock.Of<INotificationProducer>(),
            new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(ctx)),
            new OutboxPendingDispatch(ctx),
            NullLogger<CleanerNoShowCancellation>.Instance);
    }

    private CleansiaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).Options,
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
    }

    /// <summary>A 1000 card booking, paid, nobody on it, three hours before its start.</summary>
    private async Task SeedAsync()
    {
        await using var ctx = NewContext();
        ctx.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CzkId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        currency.SetNoShowCredit(250m);
        ctx.Currencies.Add(currency);
        var user = User.CreateWithPassword("no-show-early@cleansia.test", "Seed-Password-123", "Early", "NoShow");
        ctx.Users.Add(user);

        var order = Order.Create(
            customerName: "Early NoShow",
            customerEmail: "no-show-early@cleansia.test",
            customerPhone: "+420000000007",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(3),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: PaymentStatus.Paid,
            userId: user.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetMaxEmployees(1);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
