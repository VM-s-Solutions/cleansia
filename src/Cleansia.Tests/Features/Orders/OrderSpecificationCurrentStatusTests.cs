using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <see cref="OrderSpecification"/>'s status filter moved from a per-row latest-history correlated
/// subquery onto the persisted <c>Orders.CurrentStatus</c> column. These tests pin that the migrated
/// filter selects EXACTLY the rows the history-derived rule (CreatedOn desc, then Sequence desc)
/// selects, over a seeded population that includes same-timestamp Sequence ties,
/// Cancelled-after-Confirmed, and a history-less order — the expected set is independently
/// recomputed from the OrderStatusHistory rows, never from the column.
/// </summary>
public sealed class OrderSpecificationCurrentStatusTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection;

    public OrderSpecificationCurrentStatusTests()
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

    public async Task InitializeAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var baseStamp = DateTimeOffset.UtcNow.AddDays(-1);
        Seed(ctx, "spec-pending", baseStamp, OrderStatus.New, OrderStatus.Pending);
        Seed(ctx, "spec-confirmed", baseStamp, OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed);
        Seed(ctx, "spec-cancelled-after-confirmed", baseStamp, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.Cancelled);
        Seed(ctx, "spec-completed", baseStamp, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed);

        // Same-timestamp tie: Confirmed and Cancelled share one CreatedOn; Sequence decides → Cancelled.
        var tied = NewOrder("spec-tie");
        var tick = baseStamp.AddHours(2);
        AppendTrack(tied, OrderStatus.Confirmed, tick);
        AppendTrack(tied, OrderStatus.Cancelled, tick);
        ctx.Add(tied);

        // No history at all: excluded from every status filter under both the old and new rule.
        ctx.Add(NewOrder("spec-no-history"));

        await ctx.CommitAsync(CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)] // the available-orders status set
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.InProgress)]
    public async Task Status_Filter_Matches_History_Derived_Rule(params OrderStatus[] statuses)
    {
        var expected = await ExpectedIdsFromHistoryAsync(statuses);

        await using var ctx = NewContext();
        var repository = new OrderRepository(ctx);
        var specification = OrderSpecification.Create(orderStatuses: statuses);

        var actual = await repository
            .GetFiltered(specification.SatisfiedBy())
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);
        var count = await repository.GetCountAsync(specification.SatisfiedBy(), CancellationToken.None);

        Assert.Equal(expected.OrderBy(x => x), actual.OrderBy(x => x));
        Assert.Equal(expected.Count, count);
    }

    [Fact]
    public async Task Tied_Timestamps_Resolve_By_Sequence_In_The_Filter()
    {
        await using var ctx = NewContext();
        var repository = new OrderRepository(ctx);
        var cancelledSpec = OrderSpecification.Create(orderStatuses: new[] { OrderStatus.Cancelled });
        var confirmedSpec = OrderSpecification.Create(orderStatuses: new[] { OrderStatus.Confirmed });

        var cancelledIds = await repository
            .GetFiltered(cancelledSpec.SatisfiedBy())
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);
        var confirmedIds = await repository
            .GetFiltered(confirmedSpec.SatisfiedBy())
            .Select(o => o.Id)
            .ToListAsync(CancellationToken.None);

        Assert.Contains("spec-tie", cancelledIds);
        Assert.DoesNotContain("spec-tie", confirmedIds);
    }

    private async Task<List<string>> ExpectedIdsFromHistoryAsync(OrderStatus[] statuses)
    {
        await using var ctx = NewContext();
        var tracks = await ctx.Set<OrderStatusTrack>().ToListAsync(CancellationToken.None);
        return tracks
            .GroupBy(t => t.OrderId)
            .Select(g => new
            {
                OrderId = g.Key,
                Latest = g.OrderByDescending(t => t.CreatedOn).ThenByDescending(t => t.Sequence).First().Status,
            })
            .Where(x => statuses.Contains(x.Latest))
            .Select(x => x.OrderId)
            .ToList();
    }

    private static void Seed(CleansiaDbContext ctx, string orderId, DateTimeOffset baseStamp, params OrderStatus[] statuses)
    {
        var order = NewOrder(orderId);
        var stamp = baseStamp;
        foreach (var status in statuses)
        {
            AppendTrack(order, status, stamp);
            stamp = stamp.AddMinutes(20);
        }
        ctx.Add(order);
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
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-2));
        return order;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
