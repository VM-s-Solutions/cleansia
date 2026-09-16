using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
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
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0061 D10, the sweep smoke: a job has no claim, derives the tenant of every row it writes from
/// the row it read, and commits inside that scope. Two operating companies, two stale checkouts, one
/// sweep — every child row written for the CZ order carries cleansia-cz and every one for the SK order
/// carries cleansia-sk. The <c>PreCleaningReminderTenantStampTests</c> shape, on the reference sweep.
/// </summary>
[Collection("PostgresCollection")]
public sealed class CleanupStalePendingOrdersTwoOperatorsTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private const string CountryId = "country-cz-stale";
    private const string CurrencyId = "currency-czk-stale";

    private NpgsqlDataSource _dataSource = default!;
    private readonly MutableTenantProvider _tenantProvider = new();

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(fixture.GetConnectionString())
        {
            Database = "stale_orders_two_operators_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await TestTenants.EnsureCreatedWithRegistryAsync(bootstrap);
        }

        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();
        await DropOrderForeignKeysAsync(conn);
        await SeedCatalogAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }

        await _dataSource.DisposeAsync();
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private async Task SeedCatalogAsync()
    {
        _tenantProvider.SetTenantOverride(TestTenants.Default);
        await using var ctx = NewContext();

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        ctx.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        ctx.Currencies.Add(currency);
        ctx.Languages.Add(Language.Create("en", "English"));

        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    /// <summary>The Orders FKs are dropped so the fixture can name a customer without a full identity graph.</summary>
    private static async Task DropOrderForeignKeysAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand(
            """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT conname, conrelid::regclass AS tbl FROM pg_constraint
                       WHERE contype = 'f' AND conrelid = '"Orders"'::regclass
              LOOP
                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname);
              END LOOP;
            END $$;
            """,
            conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedStaleCheckoutAsync(string key, string userId, string tenantId)
    {
        var order = Order.Create(
            customerName: "Stale Customer",
            customerEmail: $"{key}@cleansia.test",
            customerPhone: "+420777000111",
            customerAddress: Address.Create("Stale St 1", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: userId);
        order.Id = $"ord-{key}";
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.Created("seed", DateTimeOffset.UtcNow.AddHours(-2));

        // The customer the cancellation feed row points at, in the same company as the order.
        var customer = User.CreateWithPassword($"{key}@cleansia.test", "Passw0rd!", "Stale", "Customer");
        customer.Id = userId;

        _tenantProvider.SetTenantOverride(tenantId);
        await using var ctx = NewContext();
        ctx.Users.Add(customer);
        ctx.Orders.Add(order);
        await ctx.CommitAsync(CancellationToken.None);
        _tenantProvider.ClearTenantOverride();
    }

    [Fact]
    public async Task Each_Operators_Cancellation_Rows_Carry_Its_Own_Company_In_One_Pass()
    {
        await SeedStaleCheckoutAsync("cz", "user-cz", TestTenants.Default);
        await SeedStaleCheckoutAsync("sk", "user-sk", TestTenants.Second);

        _tenantProvider.ClearTenantOverride();
        await using (var ctx = NewContext())
        {
            var handler = new CleanupStalePendingOrders.Handler(
                new OrderRepository(ctx),
                new CreditAccountRepository(ctx),
                new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx), new UserRepository(ctx), Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationProducer>.Instance),
                _tenantProvider,
                ctx,
                NullLogger<CleanupStalePendingOrders.Handler>.Instance);

            var result = await handler.Handle(new CleanupStalePendingOrders.Command(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value!.CancelledCount);
        }

        _tenantProvider.ClearTenantOverride();
        await using var verify = NewContext();

        foreach (var (orderId, userId, expectedTenant) in new[]
                 {
                     ("ord-cz", "user-cz", TestTenants.Default),
                     ("ord-sk", "user-sk", TestTenants.Second),
                 })
        {
            var cancelled = await verify.Set<OrderStatusTrack>().IgnoreQueryFilters()
                .SingleAsync(t => t.OrderId == orderId && t.Status == OrderStatus.Cancelled);
            Assert.Equal(expectedTenant, cancelled.TenantId);

            var feed = await verify.Set<Cleansia.Core.Domain.Notifications.UserNotification>().IgnoreQueryFilters().SingleAsync(n => n.UserId == userId);
            Assert.Equal(expectedTenant, feed.TenantId);

            var outbox = await verify.OutboxMessages.IgnoreQueryFilters()
                .SingleAsync(m => m.MessageKey.Contains(userId));
            Assert.Equal(expectedTenant, outbox.TenantId);
        }

        Assert.Empty(await verify.Set<OrderStatusTrack>().IgnoreQueryFilters().Where(t => t.TenantId == null).ToListAsync());
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
