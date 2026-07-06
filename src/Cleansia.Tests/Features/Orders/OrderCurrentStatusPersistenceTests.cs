using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The persisted <c>Orders.CurrentStatus</c> column is a denormalization of the latest
/// <c>OrderStatusHistory</c> row (CreatedOn desc, then Sequence desc), maintained at the single
/// append seam <see cref="Order.AddOrderStatus"/>. These tests pin, against a real
/// <see cref="CleansiaDbContext"/> over SQLite:
/// <list type="bullet">
/// <item>the column always equals the history-derived rule (the equivalence pin), including the
/// same-timestamp Sequence tiebreak and Cancelled-after-Confirmed;</item>
/// <item>every append through the seam updates the column (the write-seam pin), including on a
/// reloaded entity;</item>
/// <item>a legacy row with a NULL column (pre-backfill) still reads its status in memory via the
/// history fallback (the rollout-tolerance pin).</item>
/// </list>
/// Column values are asserted via a server-side projection (<c>Select(o =&gt; o.CurrentStatus)</c>)
/// so the in-memory fallback cannot mask a missing write.
/// </summary>
public sealed class OrderCurrentStatusPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public OrderCurrentStatusPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Only the status denormalization is under test; skip the full Order FK graph.
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

    private static Order NewOrder(string orderId)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid);
        order.Id = orderId;
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-1));
        return order;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private async Task SeedAsync(Order order)
    {
        await using var seed = NewContext();
        seed.Add(order);
        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task<OrderStatus?> ReadColumnAsync(string orderId)
    {
        await using var ctx = NewContext();
        return await ctx.Set<Order>()
            .Where(o => o.Id == orderId)
            .Select(o => o.CurrentStatus)
            .SingleAsync(CancellationToken.None);
    }

    private async Task<OrderStatus?> ComputeFromHistoryAsync(string orderId)
    {
        await using var ctx = NewContext();
        var tracks = await ctx.Set<OrderStatusTrack>()
            .Where(t => t.OrderId == orderId)
            .ToListAsync(CancellationToken.None);
        return tracks
            .OrderByDescending(t => t.CreatedOn)
            .ThenByDescending(t => t.Sequence)
            .FirstOrDefault()?.Status;
    }

    [Fact]
    public async Task Full_Lifecycle_Persists_Latest_Status_Equal_To_History_Rule()
    {
        await EnsureSchemaAsync();
        var order = NewOrder("cs-lifecycle-01");
        var stamp = DateTimeOffset.UtcNow.AddHours(-5);
        foreach (var status in new[]
        {
            OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed,
            OrderStatus.InProgress, OrderStatus.Completed,
        })
        {
            AppendTrack(order, status, stamp);
            stamp = stamp.AddMinutes(30);
        }
        await SeedAsync(order);

        Assert.Equal(OrderStatus.Completed, await ReadColumnAsync(order.Id));
        Assert.Equal(await ComputeFromHistoryAsync(order.Id), await ReadColumnAsync(order.Id));
    }

    [Fact]
    public async Task Cancelled_After_Confirmed_Persists_Cancelled()
    {
        await EnsureSchemaAsync();
        var order = NewOrder("cs-cancel-01");
        var baseStamp = DateTimeOffset.UtcNow.AddHours(-3);
        AppendTrack(order, OrderStatus.New, baseStamp);
        AppendTrack(order, OrderStatus.Confirmed, baseStamp.AddMinutes(10));
        AppendTrack(order, OrderStatus.Cancelled, baseStamp.AddMinutes(20));
        await SeedAsync(order);

        Assert.Equal(OrderStatus.Cancelled, await ReadColumnAsync(order.Id));
        Assert.Equal(await ComputeFromHistoryAsync(order.Id), await ReadColumnAsync(order.Id));
    }

    [Fact]
    public async Task Same_Timestamp_Transitions_Tiebreak_On_Sequence()
    {
        await EnsureSchemaAsync();
        var order = NewOrder("cs-tie-01");
        var tick = DateTimeOffset.UtcNow.AddHours(-1);
        AppendTrack(order, OrderStatus.Confirmed, tick);
        AppendTrack(order, OrderStatus.Cancelled, tick);
        await SeedAsync(order);

        Assert.Equal(OrderStatus.Cancelled, await ReadColumnAsync(order.Id));
        Assert.Equal(await ComputeFromHistoryAsync(order.Id), await ReadColumnAsync(order.Id));
    }

    [Fact]
    public async Task Backdated_Append_Does_Not_Become_Current()
    {
        await EnsureSchemaAsync();
        var order = NewOrder("cs-backdate-01");
        var baseStamp = DateTimeOffset.UtcNow.AddHours(-2);
        AppendTrack(order, OrderStatus.Completed, baseStamp);
        AppendTrack(order, OrderStatus.InProgress, baseStamp.AddMinutes(-30));
        await SeedAsync(order);

        Assert.Equal(OrderStatus.Completed, await ReadColumnAsync(order.Id));
        Assert.Equal(await ComputeFromHistoryAsync(order.Id), await ReadColumnAsync(order.Id));
    }

    [Fact]
    public async Task Append_On_Reloaded_Order_Updates_The_Column()
    {
        await EnsureSchemaAsync();
        var order = NewOrder("cs-reload-01");
        AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddHours(-2));
        await SeedAsync(order);

        await using (var ctx = NewContext())
        {
            var reloaded = await ctx.Set<Order>()
                .Include(o => o.OrderStatusHistory)
                .SingleAsync(o => o.Id == order.Id, CancellationToken.None);
            AppendTrack(reloaded, OrderStatus.Confirmed, DateTimeOffset.UtcNow);
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.Equal(OrderStatus.Confirmed, await ReadColumnAsync(order.Id));
        Assert.Equal(await ComputeFromHistoryAsync(order.Id), await ReadColumnAsync(order.Id));
    }

    [Fact]
    public async Task Legacy_Null_Column_Falls_Back_To_History_In_Memory()
    {
        await EnsureSchemaAsync();
        var order = NewOrder("cs-legacy-01");
        var baseStamp = DateTimeOffset.UtcNow.AddHours(-4);
        AppendTrack(order, OrderStatus.New, baseStamp);
        AppendTrack(order, OrderStatus.Confirmed, baseStamp.AddMinutes(15));
        await SeedAsync(order);

        await using (var ctx = NewContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE \"Orders\" SET \"CurrentStatus\" = NULL WHERE \"Id\" = {0}", order.Id);
        }

        Assert.Null(await ReadColumnAsync(order.Id));

        await using var readCtx = NewContext();
        var legacy = await readCtx.Set<Order>()
            .Include(o => o.OrderStatusHistory)
            .SingleAsync(o => o.Id == order.Id, CancellationToken.None);
        Assert.Equal(OrderStatus.Confirmed, legacy.CurrentStatus);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
