using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Dispatch;

/// <summary>
/// A loop-and-commit sweep no longer puts a push on the wire from inside its loop; each iteration writes
/// its message as an outbox row through <see cref="OutboxPendingDispatch"/> into the same scoped
/// <see cref="CleansiaDbContext"/> and commits it together with the state mutation, so a row exists if and
/// only if that iteration's state change committed. The single drainer later puts each committed row on
/// the wire exactly once — even across a re-drain, because a dispatched row is never re-claimed. Exercised
/// against a real DbContext over SQLite plus the real <see cref="OutboxDrainerService"/>; only the
/// terminal <see cref="IQueueClient"/> is a spy.
/// </summary>
public sealed class BucketBPerIterationOutboxTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly Mock<IQueueClient> _queueClient = new();

    public BucketBPerIterationOutboxTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // This suite exercises only the per-iteration commit → drain shape, not referential integrity,
        // so FK enforcement is disabled to seed bare Order rows without their full currency/address graph.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    private static Order StaleRecurringOrder(string orderId, string userId)
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
            cleaningDateTime: DateTime.UtcNow.AddMinutes(30),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: userId,
            recurringTemplateId: $"tmpl-{orderId}");
        order.Id = orderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Pending, order));
        return order;
    }

    private async Task SeedAsync(params Order[] orders)
    {
        await using var ctx = NewContext();
        ctx.Orders.AddRange(orders);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task RunSweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new AutoCancelStaleRecurringOrders.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            ctx,
            NullLogger<AutoCancelStaleRecurringOrders.Handler>.Instance);

        await handler.Handle(new AutoCancelStaleRecurringOrders.Command(), CancellationToken.None);
    }

    private OutboxDrainerService NewDrainer(CleansiaDbContext ctx)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        var deadLetterStore = new Mock<IDeadLetterStore>();
        return new OutboxDrainerService(
            new OutboxMessageRepository(ctx),
            _queueClient.Object,
            deadLetterStore.Object,
            tenantProvider.Object,
            new StubConfig { BatchSize = 100, MaxAttempts = 5, BaseBackoffSeconds = 30, LeaseSeconds = 120 },
            NullLogger<OutboxDrainerService>.Instance);
    }

    private async Task<int> DrainAsync()
    {
        await using var ctx = NewContext();
        return await NewDrainer(ctx).DrainOnceAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Each_Per_Iteration_Commit_Writes_Exactly_One_Outbox_Row()
    {
        await EnsureSchemaAsync();
        await SeedAsync(
            StaleRecurringOrder("01HZX9N6M7Q8R9S0T1V2W3X401", "user-1"),
            StaleRecurringOrder("01HZX9N6M7Q8R9S0T1V2W3X402", "user-2"));

        await RunSweepAsync();

        await using var readCtx = NewContext();
        var rows = await readCtx.OutboxMessages.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(QueueNames.NotificationsDispatch, r.QueueName));
        Assert.All(rows, r => Assert.Equal(OutboxMessageStatus.Pending, r.Status));
    }

    [Fact]
    public async Task The_Sweep_Never_Touches_The_Wire_Directly()
    {
        await EnsureSchemaAsync();
        await SeedAsync(StaleRecurringOrder("01HZX9N6M7Q8R9S0T1V2W3X403", "user-1"));

        await RunSweepAsync();

        _queueClient.Verify(
            q => q.SendAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Committed_Row_Drains_To_The_Wire_Exactly_Once()
    {
        await EnsureSchemaAsync();
        await SeedAsync(StaleRecurringOrder("01HZX9N6M7Q8R9S0T1V2W3X404", "user-1"));

        await RunSweepAsync();
        var dispatched = await DrainAsync();

        Assert.Equal(1, dispatched);
        _queueClient.Verify(
            q => q.SendAsync(QueueNames.NotificationsDispatch, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Second_Drain_Does_Not_Re_Send_The_Terminal_Effect()
    {
        await EnsureSchemaAsync();
        await SeedAsync(StaleRecurringOrder("01HZX9N6M7Q8R9S0T1V2W3X405", "user-1"));

        await RunSweepAsync();
        await DrainAsync();
        var secondDrain = await DrainAsync();

        Assert.Equal(0, secondDrain);
        _queueClient.Verify(
            q => q.SendAsync(QueueNames.NotificationsDispatch, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task The_Drained_Body_Carries_The_Cancelled_Push_For_Its_User()
    {
        await EnsureSchemaAsync();
        await SeedAsync(StaleRecurringOrder("01HZX9N6M7Q8R9S0T1V2W3X406", "user-77"));

        await RunSweepAsync();

        await using var readCtx = NewContext();
        var row = Assert.Single(await readCtx.OutboxMessages.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(
            MessageKeys.Push("user-77", NotificationEventCatalog.OrderCancelled, "01HZX9N6M7Q8R9S0T1V2W3X406"),
            row.MessageKey);
        Assert.Contains(NotificationEventCatalog.OrderCancelled, row.Body);
        Assert.Contains("user-77", row.Body);
    }

    private sealed class StubConfig : IOutboxDrainerConfig
    {
        public int BatchSize { get; set; }
        public int MaxAttempts { get; set; }
        public int BaseBackoffSeconds { get; set; }
        public int LeaseSeconds { get; set; }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
