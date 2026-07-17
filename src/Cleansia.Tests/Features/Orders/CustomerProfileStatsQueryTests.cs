using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Pins <see cref="OrderRepository.GetCustomerProfileStatsAsync"/> (T-0392) against a real
/// <see cref="CleansiaDbContext"/> over SQLite. Total bookings = ALL of the user's orders. Total
/// savings = the tier + promo + membership discounts of the user's REALIZED orders — not Cancelled,
/// not (fully or partially) Refunded — summed in ONE currency (the most recent realized order's),
/// so a foreign-currency order is excluded rather than added as unlike units. A null-status order
/// still counts; a cash (non-Paid) order still counts; another user's orders never leak in; and a
/// user with no orders yields (0, 0, null).
/// </summary>
public sealed class CustomerProfileStatsQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CustomerProfileStatsQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    [Fact]
    public async Task Counts_All_Orders_But_Sums_Realized_SameCurrency_Discounts_Only()
    {
        await EnsureSchemaAsync();
        const string userId = "user-stats-1";

        await using (var seed = NewContext())
        {
            seed.Add(NewCurrency("czk", "CZK"));
            seed.Add(NewCurrency("eur", "EUR"));

            // tier 100, Completed, CZK, Paid, 5d ago       -> realized, saves 100
            seed.Add(NewOrder("stats-a", userId, tier: 100m, status: OrderStatus.Completed, createdDaysAgo: 5));
            // promo 50, Confirmed, CZK, CASH/Pending, 4d   -> realized (cash still counts), saves 50
            seed.Add(NewOrder("stats-b", userId, promo: 50m, status: OrderStatus.Confirmed,
                paymentStatus: PaymentStatus.Pending, createdDaysAgo: 4));
            // membership 30, InProgress, CZK, Paid, 3d      -> realized (most recent realized -> CZK), saves 30
            seed.Add(NewOrder("stats-c", userId, membership: 30m, status: OrderStatus.InProgress, createdDaysAgo: 3));
            // tier 200, Cancelled, CZK, 6d                  -> booking only (cancelled)
            seed.Add(NewOrder("stats-d", userId, tier: 200m, status: OrderStatus.Cancelled, createdDaysAgo: 6));
            // no discount, null status, CZK, 7d             -> realized, saves 0
            seed.Add(NewOrder("stats-e", userId, status: null, createdDaysAgo: 7));
            // tier 500, Completed, CZK, REFUNDED, 2d (newest overall) -> booking only (refunded, not saved)
            seed.Add(NewOrder("stats-f", userId, tier: 500m, status: OrderStatus.Completed,
                paymentStatus: PaymentStatus.Refunded, createdDaysAgo: 2));
            // tier 77, Completed, EUR, Paid, 10d            -> booking only (foreign currency, not summed)
            seed.Add(NewOrder("stats-g", userId, tier: 77m, status: OrderStatus.Completed,
                currencyId: "eur", createdDaysAgo: 10));
            // a different user's discounted order           -> must not leak in
            seed.Add(NewOrder("stats-other", "user-other", tier: 999m, status: OrderStatus.Completed));

            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var stats = await new OrderRepository(ctx).GetCustomerProfileStatsAsync(userId, CancellationToken.None);

        Assert.Equal(7, stats.TotalBookings);
        Assert.Equal(180m, stats.TotalSavings); // 100 + 50 (cash) + 30 + 0; refunded 500 & EUR 77 & cancelled 200 excluded
        Assert.Equal("CZK", stats.SavingsCurrencyCode);
    }

    [Fact]
    public async Task Empty_For_A_User_With_No_Orders()
    {
        await EnsureSchemaAsync();
        await using var ctx = NewContext();

        var stats = await new OrderRepository(ctx).GetCustomerProfileStatsAsync("nobody", CancellationToken.None);

        Assert.Equal(0, stats.TotalBookings);
        Assert.Equal(0m, stats.TotalSavings);
        Assert.Null(stats.SavingsCurrencyCode);
    }

    private static Currency NewCurrency(string id, string code)
    {
        var currency = Currency.Create(code, code == "EUR" ? "€" : "Kč", code, 1m);
        currency.Id = id;
        currency.Created("system", DateTimeOffset.UtcNow.AddDays(-30));
        return currency;
    }

    private static Order NewOrder(
        string orderId,
        string? userId,
        decimal? tier = null,
        decimal? promo = null,
        decimal? membership = null,
        OrderStatus? status = OrderStatus.Completed,
        string currencyId = "czk",
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        int createdDaysAgo = 2)
    {
        var order = Order.Create(
            customerName: "Stats Customer",
            customerEmail: "stats@cleansia.test",
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Stat St 1", "Praha", "14000", "cz"),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: currencyId,
            paymentStatus: paymentStatus,
            userId: userId,
            tierDiscountAmount: tier,
            promoDiscountAmount: promo,
            membershipDiscountAmount: membership);
        order.Id = orderId;
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-createdDaysAgo));

        if (status is not null)
        {
            var track = OrderStatusTrack.Create(status.Value, order);
            track.Created("system", DateTimeOffset.UtcNow.AddHours(-6));
            order.AddOrderStatus(track);
        }

        return order;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
