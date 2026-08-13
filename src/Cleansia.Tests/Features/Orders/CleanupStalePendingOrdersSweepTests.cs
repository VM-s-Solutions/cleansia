using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The stale-pending sweep exists to retract ABANDONED CHECKOUTS. A recurring occurrence is not one:
/// the materializer creates it up to 7 days before its slot and it stays Pending until the customer
/// confirms it, so the sweep's "Pending for over an hour" heuristic killed every card occurrence a
/// week before the customer was ever asked to pay (cash survived only because the sweep filters on
/// PaymentType.Card — the feature worked or not depending on how the customer paid).
///
/// Exercised against a real DbContext over SQLite with the real repository, so the exclusion is
/// pinned where it lives — in the SQL predicate — rather than in a mock's return value.
/// </summary>
public sealed class CleanupStalePendingOrdersSweepTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FixedTenantProvider _tenantProvider = new(null);

    public CleanupStalePendingOrdersSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // This suite exercises the sweep's predicate, not referential integrity, so FK enforcement is
        // off to seed bare Order rows without their full currency/address graph.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static Order PendingOrder(
        string orderId,
        string userId,
        PaymentType paymentType,
        string? recurringTemplateId,
        TimeSpan createdAgo,
        TimeSpan cleaningIn)
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
            cleaningDateTime: DateTime.UtcNow.Add(cleaningIn),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: userId,
            recurringTemplateId: recurringTemplateId);
        order.Id = orderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));

        // An explicit creation audit survives CommitAsync's auto-stamp, which is the only way to seed
        // a row that is already older than the sweep's cutoff.
        order.Created("seed", DateTimeOffset.UtcNow.Subtract(createdAgo));
        return order;
    }

    private static Order RecurringCardOccurrence(string orderId, string userId) =>
        PendingOrder(
            orderId,
            userId,
            PaymentType.Card,
            recurringTemplateId: $"tmpl-{orderId}",
            createdAgo: TimeSpan.FromHours(2),
            cleaningIn: TimeSpan.FromDays(7));

    private static Order AbandonedOneOffCardCheckout(string orderId, string userId) =>
        PendingOrder(
            orderId,
            userId,
            PaymentType.Card,
            recurringTemplateId: null,
            createdAgo: TimeSpan.FromHours(2),
            cleaningIn: TimeSpan.FromDays(2));

    private async Task SeedAsync(params Order[] orders)
    {
        await using var ctx = NewContext();
        ctx.Orders.AddRange(orders);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<CleanupStalePendingOrders.Response> RunSweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new CleanupStalePendingOrders.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            _tenantProvider,
            ctx,
            NullLogger<CleanupStalePendingOrders.Handler>.Instance);

        var result = await handler.Handle(new CleanupStalePendingOrders.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task RunRecurringSweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new AutoCancelStaleRecurringOrders.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            _tenantProvider,
            ctx,
            NullLogger<AutoCancelStaleRecurringOrders.Handler>.Instance);

        await handler.Handle(new AutoCancelStaleRecurringOrders.Command(), CancellationToken.None);
    }

    private async Task<Order> ReadOrderAsync(string orderId)
    {
        await using var ctx = NewContext();
        return await ctx.Orders
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .FirstAsync(o => o.Id == orderId);
    }

    private async Task<List<UserNotification>> ReadFeedAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task<List<string>> ReadOutboxKeysAsync()
    {
        await using var ctx = NewContext();
        return await ctx.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
            .Select(m => m.MessageKey)
            .ToListAsync();
    }

    [Fact]
    public async Task A_Recurring_Card_Occurrence_Materialized_Seven_Days_Out_Survives_The_Sweep()
    {
        await EnsureSchemaAsync();
        await SeedAsync(RecurringCardOccurrence("01HZX9N6M7Q8R9S0T1V2W3Y401", "user-1"));

        var response = await RunSweepAsync();

        Assert.Equal(0, response.CancelledCount);
        var order = await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y401");
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.NotEqual(OrderStatus.Cancelled, order.CurrentStatus);
    }

    [Fact]
    public async Task An_Abandoned_One_Off_Card_Checkout_Is_Still_Cancelled()
    {
        await EnsureSchemaAsync();
        await SeedAsync(AbandonedOneOffCardCheckout("01HZX9N6M7Q8R9S0T1V2W3Y402", "user-2"));

        var response = await RunSweepAsync();

        Assert.Equal(1, response.CancelledCount);
        var order = await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y402");
        Assert.Equal(PaymentStatus.Failed, order.PaymentStatus);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
    }

    [Fact]
    public async Task The_Sweep_Separates_The_Two_Populations_In_One_Pass()
    {
        await EnsureSchemaAsync();
        await SeedAsync(
            RecurringCardOccurrence("01HZX9N6M7Q8R9S0T1V2W3Y403", "user-3"),
            AbandonedOneOffCardCheckout("01HZX9N6M7Q8R9S0T1V2W3Y404", "user-4"));

        var response = await RunSweepAsync();

        Assert.Equal(1, response.CancelledCount);
        Assert.Equal(PaymentStatus.Pending, (await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y403")).PaymentStatus);
        Assert.Equal(PaymentStatus.Failed, (await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y404")).PaymentStatus);
    }

    [Fact]
    public async Task A_Card_Checkout_Still_Inside_The_One_Hour_Window_Survives()
    {
        await EnsureSchemaAsync();
        await SeedAsync(PendingOrder(
            "01HZX9N6M7Q8R9S0T1V2W3Y405",
            "user-5",
            PaymentType.Card,
            recurringTemplateId: null,
            createdAgo: TimeSpan.FromMinutes(10),
            cleaningIn: TimeSpan.FromDays(2)));

        var response = await RunSweepAsync();

        Assert.Equal(0, response.CancelledCount);
        Assert.Equal(PaymentStatus.Pending, (await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y405")).PaymentStatus);
    }

    [Fact]
    public async Task A_Cash_Order_Is_Never_This_Sweeps_Business()
    {
        await EnsureSchemaAsync();
        await SeedAsync(PendingOrder(
            "01HZX9N6M7Q8R9S0T1V2W3Y406",
            "user-6",
            PaymentType.Cash,
            recurringTemplateId: null,
            createdAgo: TimeSpan.FromHours(2),
            cleaningIn: TimeSpan.FromDays(2)));

        var response = await RunSweepAsync();

        Assert.Equal(0, response.CancelledCount);
        Assert.Equal(PaymentStatus.Pending, (await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y406")).PaymentStatus);
    }

    [Fact]
    public async Task A_Surviving_Recurring_Occurrence_Is_Retracted_By_Its_Own_Sweep_When_Its_Slot_Nears()
    {
        await EnsureSchemaAsync();
        await SeedAsync(PendingOrder(
            "01HZX9N6M7Q8R9S0T1V2W3Y407",
            "user-7",
            PaymentType.Card,
            recurringTemplateId: "tmpl-7",
            createdAgo: TimeSpan.FromHours(2),
            cleaningIn: TimeSpan.FromMinutes(30)));

        await RunSweepAsync();
        Assert.Equal(PaymentStatus.Pending, (await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y407")).PaymentStatus);

        await RunRecurringSweepAsync();

        Assert.Equal(OrderStatus.Cancelled, (await ReadOrderAsync("01HZX9N6M7Q8R9S0T1V2W3Y407")).CurrentStatus);
    }

    [Fact]
    public async Task A_Cancelled_Customer_Is_Told()
    {
        await EnsureSchemaAsync();
        await SeedAsync(AbandonedOneOffCardCheckout("01HZX9N6M7Q8R9S0T1V2W3Y408", "user-8"));

        await RunSweepAsync();

        var feedRow = Assert.Single(await ReadFeedAsync());
        Assert.Equal("user-8", feedRow.UserId);
        Assert.Equal(NotificationEventCatalog.OrderCancelled, feedRow.EventKey);
        Assert.Contains("01HZX9N6M7Q8R9S0T1V2W3Y408", feedRow.ArgsJson);

        var outboxKey = Assert.Single(await ReadOutboxKeysAsync());
        Assert.Equal(
            MessageKeys.Push("user-8", NotificationEventCatalog.OrderCancelled, "01HZX9N6M7Q8R9S0T1V2W3Y408"),
            outboxKey);
    }

    [Fact]
    public async Task A_Survivor_Produces_No_Cancellation_Notice()
    {
        await EnsureSchemaAsync();
        await SeedAsync(RecurringCardOccurrence("01HZX9N6M7Q8R9S0T1V2W3Y409", "user-9"));

        await RunSweepAsync();

        Assert.Empty(await ReadFeedAsync());
        Assert.Empty(await ReadOutboxKeysAsync());
    }

    [Fact]
    public async Task A_Re_Run_Neither_Re_Cancels_Nor_Re_Notifies()
    {
        await EnsureSchemaAsync();
        await SeedAsync(AbandonedOneOffCardCheckout("01HZX9N6M7Q8R9S0T1V2W3Y410", "user-10"));

        await RunSweepAsync();
        var second = await RunSweepAsync();

        Assert.Equal(0, second.CancelledCount);
        Assert.Single(await ReadFeedAsync());
        Assert.Single(await ReadOutboxKeysAsync());
    }

    [Fact]
    public async Task Each_Tenant_Groups_Rows_Are_Stamped_With_Their_Own_Tenant()
    {
        await EnsureSchemaAsync();

        var tenanted = AbandonedOneOffCardCheckout("01HZX9N6M7Q8R9S0T1V2W3Y411", "user-11");
        tenanted.TenantId = "tenant-a";
        var legacy = AbandonedOneOffCardCheckout("01HZX9N6M7Q8R9S0T1V2W3Y412", "user-12");
        await SeedAsync(tenanted, legacy);

        await RunSweepAsync();

        var feed = await ReadFeedAsync();
        Assert.Equal("tenant-a", Assert.Single(feed, f => f.UserId == "user-11").TenantId);
        Assert.Null(Assert.Single(feed, f => f.UserId == "user-12").TenantId);

        await using var ctx = NewContext();
        var tracks = await ctx.Set<OrderStatusTrack>()
            .IgnoreQueryFilters()
            .Where(t => t.Status == OrderStatus.Cancelled)
            .ToListAsync();
        Assert.Equal("tenant-a", Assert.Single(tracks, t => t.OrderId == "01HZX9N6M7Q8R9S0T1V2W3Y411").TenantId);
        Assert.Null(Assert.Single(tracks, t => t.OrderId == "01HZX9N6M7Q8R9S0T1V2W3Y412").TenantId);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
