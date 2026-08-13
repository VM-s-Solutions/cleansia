using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The booking-confirmation screen promises every customer "We'll remind you 1 hour before". This suite
/// pins the sweep that keeps it: who qualifies, that the reminder fires exactly once, and that the
/// window is closed at both ends.
///
/// <para>Qualification is read off BOTH lifecycle axes, never the fulfilment one alone.
/// <c>Confirmed</c> is overloaded ("money settled" OR "cleaner assigned"), so the status term is
/// conjoined with an assignment row — telling a customer their cleaning starts in an hour when nobody
/// has taken the job is worse than saying nothing. The money term is <c>OrderAvailability</c>'s,
/// specialised to one-off orders: a card order still <c>Pending</c> is minutes from being retracted by
/// <c>CleanupStalePendingOrders</c>.</para>
///
/// <para>Exercised against a real DbContext over SQLite with the real repository and the real notify
/// seam, so the eligibility rule is pinned where it lives — in the SQL predicate — and the dispatch
/// assertions read the outbox row the production path actually writes.</para>
/// </summary>
public sealed class PreCleaningReminderSweepTests : IDisposable
{
    private const string CustomerId = "user-precleaning";
    private const string CleanerId = "employee-precleaning";

    private readonly SqliteConnection _connection;
    private readonly FixedTenantProvider _tenantProvider = new(null);

    public PreCleaningReminderSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // This suite exercises the sweep's predicate, not referential integrity, so FK enforcement is
        // off to seed bare Order / OrderEmployee rows without their full employee and address graph.
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

    private static Order SeededOrder(
        string orderId,
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        TimeSpan cleaningIn,
        string? recurringTemplateId = null,
        string? userId = CustomerId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.Add(cleaningIn),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: paymentStatus,
            userId: userId,
            recurringTemplateId: recurringTemplateId);
        order.Id = orderId;
        order.CalculateRequiredEmployees(spareSeats: 0);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (status != OrderStatus.New)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        }

        return order;
    }

    /// <summary>The default fixture: a one-off cash order an hour out that a cleaner has taken.</summary>
    private static Order DueOrder(string orderId, TimeSpan? cleaningIn = null) =>
        SeededOrder(
            orderId,
            OrderStatus.Confirmed,
            PaymentType.Cash,
            PaymentStatus.Pending,
            cleaningIn ?? TimeSpan.FromMinutes(60));

    private async Task SeedAsync(params Order[] orders)
    {
        await using var ctx = NewContext();
        ctx.Orders.AddRange(orders);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task AssignCleanerAsync(string orderId, string employeeId = CleanerId)
    {
        await using var ctx = NewContext();
        await ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"OrderEmployees\" (\"Id\", \"OrderId\", \"EmployeeId\", \"IsActive\", \"SeatOrdinal\") VALUES ({0}, {1}, {2}, 1, 0)",
            $"oe-{orderId}", orderId, employeeId);
    }

    private async Task<SendPreCleaningReminders.Response> RunSweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new SendPreCleaningReminders.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            _tenantProvider,
            ctx,
            NullLogger<SendPreCleaningReminders.Handler>.Instance);

        var result = await handler.Handle(new SendPreCleaningReminders.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task<Order> ReadOrderAsync(string orderId)
    {
        await using var ctx = NewContext();
        return await ctx.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);
    }

    private async Task<List<OutboxMessageProbe>> ReadRemindersAsync()
    {
        await using var ctx = NewContext();
        return await ctx.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
            .Select(m => new OutboxMessageProbe(m.MessageKey, m.Body, m.TenantId))
            .ToListAsync();
    }

    private async Task<List<UserNotification>> ReadFeedAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    [Fact]
    public async Task An_Order_An_Hour_Out_With_A_Cleaner_On_It_Gets_Exactly_One_Reminder()
    {
        await EnsureSchemaAsync();
        await SeedAsync(DueOrder("01HZY0000000000000000000A1"));
        await AssignCleanerAsync("01HZY0000000000000000000A1");

        var response = await RunSweepAsync();

        Assert.Equal(1, response.RemindersSent);
        Assert.Equal(1, response.Considered);

        var reminder = Assert.Single(await ReadRemindersAsync());
        Assert.Equal(
            MessageKeys.Push(CustomerId, NotificationEventCatalog.OrderStartingSoon, "01HZY0000000000000000000A1"),
            reminder.MessageKey);
        Assert.NotNull((await ReadOrderAsync("01HZY0000000000000000000A1")).PreCleaningReminderSentAt);
    }

    [Fact]
    public async Task The_Same_Order_Swept_Twice_Gets_One_Reminder_Not_Two()
    {
        await EnsureSchemaAsync();
        await SeedAsync(DueOrder("01HZY0000000000000000000A2"));
        await AssignCleanerAsync("01HZY0000000000000000000A2");

        var first = await RunSweepAsync();
        var second = await RunSweepAsync();

        Assert.Equal(1, first.RemindersSent);
        Assert.Equal(0, second.RemindersSent);
        Assert.Equal(0, second.Considered);
        Assert.Single(await ReadRemindersAsync());
    }

    /// <summary>
    /// The sweep's <c>== null</c> filter should mean the stamp is never written twice, so this pins the
    /// entity's own guarantee rather than the query's: if it were ever reached again, the first instant
    /// stands and the order does not re-enter the window.
    /// </summary>
    [Fact]
    public void The_Reminder_Stamp_Keeps_The_First_Instant()
    {
        var order = DueOrder("01HZY0000000000000000000A8");
        var firstSend = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc);

        order.MarkPreCleaningReminderSent(firstSend);
        order.MarkPreCleaningReminderSent(firstSend.AddHours(1));

        Assert.Equal(firstSend, order.PreCleaningReminderSentAt);
    }

    /// <summary>
    /// One order per case, so a term dropped from the status predicate fails exactly the state it
    /// re-admits instead of hiding behind a sibling in a shared fixture.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.New, false)]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.OnTheWay, false)]
    [InlineData(OrderStatus.InProgress, false)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task Only_A_Confirmed_Order_Is_Reminded(OrderStatus status, bool expectReminder)
    {
        await EnsureSchemaAsync();
        var orderId = $"01HZY000000000000000000S{(int)status}";
        await SeedAsync(SeededOrder(
            orderId, status, PaymentType.Cash, PaymentStatus.Pending, TimeSpan.FromMinutes(60)));
        await AssignCleanerAsync(orderId);

        var response = await RunSweepAsync();

        Assert.Equal(expectReminder ? 1 : 0, response.RemindersSent);
        Assert.Equal(expectReminder, (await ReadOrderAsync(orderId)).PreCleaningReminderSentAt is not null);
    }

    /// <summary>
    /// The lifecycle's own warning, as a test: <c>Confirmed</c> means "money settled" OR "cleaner
    /// assigned", and the Stripe webhook writes it with nobody on the job. Reminding that customer
    /// promises an arrival the platform has not secured.
    /// </summary>
    [Fact]
    public async Task A_Confirmed_Order_Nobody_Has_Taken_Is_Not_Reminded()
    {
        await EnsureSchemaAsync();
        await SeedAsync(SeededOrder(
            "01HZY0000000000000000000A3",
            OrderStatus.Confirmed,
            PaymentType.Card,
            PaymentStatus.Paid,
            TimeSpan.FromMinutes(60)));

        var response = await RunSweepAsync();

        Assert.Equal(0, response.RemindersSent);
        Assert.Empty(await ReadRemindersAsync());
    }

    /// <summary>
    /// The money axis. A one-off card order still <c>Pending</c> is what
    /// <c>CleanupStalePendingOrders</c> cancels on its next 15-minute tick, so a reminder would
    /// promise a clean the platform is about to retract. Cash carries no such retractor.
    /// </summary>
    [Theory]
    [InlineData(PaymentType.Card, PaymentStatus.Pending, false)]
    [InlineData(PaymentType.Card, PaymentStatus.Paid, true)]
    [InlineData(PaymentType.Cash, PaymentStatus.Pending, true)]
    public async Task An_Order_Something_Can_Still_Retract_Is_Not_Reminded(
        PaymentType paymentType, PaymentStatus paymentStatus, bool expectReminder)
    {
        await EnsureSchemaAsync();
        var orderId = $"01HZY00000000000000000M{(int)paymentType}{(int)paymentStatus}";
        await SeedAsync(SeededOrder(
            orderId, OrderStatus.Confirmed, paymentType, paymentStatus, TimeSpan.FromMinutes(60)));
        await AssignCleanerAsync(orderId);

        var response = await RunSweepAsync();

        Assert.Equal(expectReminder ? 1 : 0, response.RemindersSent);
    }

    /// <summary>
    /// A recurring occurrence has its own reminder — the 24h-ahead confirm prompt — and its own copy
    /// on its own screen. Out of scope here, and its stamp is a different column.
    /// </summary>
    [Fact]
    public async Task A_Recurring_Occurrence_Is_Not_This_Sweeps_Business()
    {
        await EnsureSchemaAsync();
        await SeedAsync(SeededOrder(
            "01HZY0000000000000000000A4",
            OrderStatus.Confirmed,
            PaymentType.Cash,
            PaymentStatus.Paid,
            TimeSpan.FromMinutes(60),
            recurringTemplateId: "tmpl-weekly"));
        await AssignCleanerAsync("01HZY0000000000000000000A4");

        var response = await RunSweepAsync();

        Assert.Equal(0, response.RemindersSent);
        Assert.Null((await ReadOrderAsync("01HZY0000000000000000000A4")).RecurringReminderSentAt);
    }

    [Fact]
    public async Task A_Guest_Order_With_No_Account_Is_Skipped()
    {
        await EnsureSchemaAsync();
        await SeedAsync(SeededOrder(
            "01HZY0000000000000000000A5",
            OrderStatus.Confirmed,
            PaymentType.Cash,
            PaymentStatus.Pending,
            TimeSpan.FromMinutes(60),
            userId: null));
        await AssignCleanerAsync("01HZY0000000000000000000A5");

        Assert.Equal(0, (await RunSweepAsync()).RemindersSent);
    }

    /// <summary>
    /// The window is closed at BOTH ends. Too early is merely premature, but too late is a lie: the
    /// copy says an hour, and a reminder cannot be un-sent. A tick the host missed therefore costs
    /// silence, which is the honest degradation.
    ///
    /// <para>The cases straddle each bound by a minute rather than sitting on it: the fixture's clock
    /// and the handler's are a few milliseconds apart, so an exactly-on-the-endpoint case would assert
    /// the elapsed time of the seed rather than the predicate.</para>
    /// </summary>
    [Theory]
    [InlineData(180, false)]
    [InlineData(71, false)]
    [InlineData(69, true)]
    [InlineData(60, true)]
    [InlineData(56, true)]
    [InlineData(54, false)]
    [InlineData(30, false)]
    [InlineData(-10, false)]
    public async Task Only_Orders_Inside_The_Promised_Hour_Are_Reminded(int cleaningInMinutes, bool expectReminder)
    {
        await EnsureSchemaAsync();
        var orderId = $"01HZY0000000000000000W{cleaningInMinutes + 100}";
        await SeedAsync(DueOrder(orderId, TimeSpan.FromMinutes(cleaningInMinutes)));
        await AssignCleanerAsync(orderId);

        var response = await RunSweepAsync();

        Assert.Equal(expectReminder ? 1 : 0, response.RemindersSent);
    }

    /// <summary>
    /// The push args stay inside the closed lock-screen allowlist: <c>orderNumber</c> renders,
    /// <c>orderId</c> is the deep link, and no internal id or raw enum value is along for the ride.
    /// </summary>
    [Fact]
    public async Task The_Reminder_Carries_Only_The_Order_Number_And_The_Deep_Link()
    {
        await EnsureSchemaAsync();
        var order = DueOrder("01HZY0000000000000000000A6");
        await SeedAsync(order);
        await AssignCleanerAsync("01HZY0000000000000000000A6");

        await RunSweepAsync();

        var args = JsonDocument.Parse(Assert.Single(await ReadRemindersAsync()).Body)
            .RootElement.GetProperty("payload").GetProperty("args");

        Assert.Equal(
            ["orderId", "orderNumber"],
            args.EnumerateObject().Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("01HZY0000000000000000000A6", args.GetProperty("orderId").GetString());
        Assert.Equal(order.DisplayOrderNumber, args.GetProperty("orderNumber").GetString());
    }

    /// <summary>
    /// The key is dispatched but deliberately not a feed event yet: the badge counts every keyset row
    /// and no client carries copy for it, so an inbox row would be an invisible unread.
    /// </summary>
    [Fact]
    public async Task No_Inbox_Row_Is_Written_While_The_Clients_Cannot_Render_It()
    {
        await EnsureSchemaAsync();
        await SeedAsync(DueOrder("01HZY0000000000000000000A7"));
        await AssignCleanerAsync("01HZY0000000000000000000A7");

        await RunSweepAsync();

        Assert.Single(await ReadRemindersAsync());
        Assert.Empty(await ReadFeedAsync());
    }

    private sealed record OutboxMessageProbe(string MessageKey, string Body, string? TenantId);

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
