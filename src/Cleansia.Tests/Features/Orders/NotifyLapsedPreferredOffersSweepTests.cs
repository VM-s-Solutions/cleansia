using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
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
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0045 D6 / AC2 — the sweep <b>ANNOUNCES</b>, and releases nothing. Exercised against a real
/// DbContext over SQLite with the real repository, so the predicate is pinned where it lives rather
/// than in a mock's return value.
///
/// <para>The property that matters most is the one asserted without running the sweep at all: the
/// reservation ends because the clock passed it, so a dead timer costs a prompt and never a booking.
/// Anything that made expiry an EVENT — a status flip, a cleared pair, an <c>IsActive</c> toggle —
/// would leave that case red.</para>
/// </summary>
public sealed class NotifyLapsedPreferredOffersSweepTests : IDisposable
{
    private const string BeneficiaryId = "employee-lapse-beneficiary";
    private const string RivalId = "employee-lapse-rival";

    private readonly SqliteConnection _connection;
    private readonly FixedTenantProvider _tenantProvider = new(null);

    public NotifyLapsedPreferredOffersSweepTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    // ── The release: a clock comparison, with no actor ──

    /// <summary>
    /// AC2, and the reason the sweep may fail without costing anything. The SAME row, the same process,
    /// no sweep run and no write in between — only a later clock — and the order is open to every
    /// cleaner on the board.
    /// </summary>
    [Fact]
    public async Task Silence_Releases_The_Job_With_No_Sweep_Run_At_All()
    {
        await EnsureSchemaAsync();
        var lapsesAt = DateTime.UtcNow.AddMinutes(30);
        await SeedAsync(ReservedOrder("01HZL0000000000000000001", lapsesAt: lapsesAt));

        var duringReservation = await OpenToRivalAsync(lapsesAt.AddMinutes(-1));
        var afterReservation = await OpenToRivalAsync(lapsesAt.AddMinutes(1));

        Assert.DoesNotContain("01HZL0000000000000000001", duringReservation);
        Assert.Contains("01HZL0000000000000000001", afterReservation);
    }

    // ── The announcement: exactly one message, exactly one write ──

    [Fact]
    public async Task A_Lapsed_Reservation_Prompts_The_Customer_Once()
    {
        await EnsureSchemaAsync();
        await SeedAsync(ReservedOrder("01HZL0000000000000000002", lapsesAt: DateTime.UtcNow.AddMinutes(-1)));

        var response = await RunSweepAsync();

        Assert.Equal(1, response.NotifiedCount);
        Assert.Equal(
            MessageKeys.Push(
                "user-customer",
                NotificationEventCatalog.PreferredOfferClosed,
                "01HZL0000000000000000002"),
            Assert.Single(await ReadOutboxKeysAsync()));

        // No inbox row, deliberately: the key is held out of NotificationFeedEventKeys.Customer until
        // both customer clients render it, because the unread badge counts every row in the keyset and
        // the apps drop what they cannot draw. The PUSH is unaffected — it dispatches off the event
        // catalog, not the feed keyset.
        Assert.Empty(await ReadFeedAsync());
    }

    /// <summary>
    /// Verify #4 — the ONLY <c>Order</c> property the sweep assigns is the receipt. The hold pair in
    /// particular must survive intact: <c>NewJobsDigestService.ApplyFreshness</c> reads it to decide a
    /// lapsed order is new again to every other cleaner, so clearing it here would drop the order out
    /// of the notification channel permanently.
    /// </summary>
    [Fact]
    public async Task The_Sweeps_Only_Write_Is_The_Receipt()
    {
        await EnsureSchemaAsync();
        var lapsedAt = DateTime.UtcNow.AddMinutes(-1);
        await SeedAsync(ReservedOrder("01HZL0000000000000000003", lapsesAt: lapsedAt));

        await RunSweepAsync();

        var order = await ReadOrderAsync("01HZL0000000000000000003");
        Assert.NotNull(order.PreferredOfferLapseNotifiedAt);
        Assert.Equal(BeneficiaryId, order.PreferredEmployeeId);
        Assert.Equal(lapsedAt, order.PreferredHoldUntilUtc!.Value, TimeSpan.FromSeconds(1));
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(2, order.OrderStatusHistory.Count);
        Assert.Empty(order.AssignedEmployees);
        Assert.Equal(1, order.PreferredOfferRound);
    }

    [Fact]
    public async Task A_Re_Run_Does_Not_Prompt_Twice()
    {
        await EnsureSchemaAsync();
        await SeedAsync(ReservedOrder("01HZL0000000000000000004", lapsesAt: DateTime.UtcNow.AddMinutes(-1)));

        await RunSweepAsync();
        var second = await RunSweepAsync();

        Assert.Equal(0, second.NotifiedCount);
        Assert.Single(await ReadOutboxKeysAsync());
    }

    /// <summary>
    /// Verify #18, and the whole of the neutrality guarantee: the 5-minute lapse and the immediate
    /// decline say <b>exactly the same thing</b> — same event key, so the same client template renders,
    /// and the same arg names, so the same sentence comes out. A per-path string would re-introduce the
    /// disclosure the neutral line exists to prevent: the customer would learn which way the offer
    /// ended, which is a statement about what a named worker did.
    /// </summary>
    [Fact]
    public async Task The_Lapse_And_The_Decline_Say_Exactly_The_Same_Thing()
    {
        await EnsureSchemaAsync();
        await SeedAsync(
            ReservedOrder("01HZL0000000000000000012", lapsesAt: DateTime.UtcNow.AddMinutes(-1)),
            ReservedOrder("01HZL0000000000000000013", lapsesAt: DateTime.UtcNow.AddHours(3)));

        await RunSweepAsync();
        await RunDeclineAsync("01HZL0000000000000000013");

        var messages = await ReadOutboxAsync();
        var lapse = ArgsOf(Assert.Single(messages, m => m.Body.Contains("01HZL0000000000000000012")));
        var decline = ArgsOf(Assert.Single(messages, m => m.Body.Contains("01HZL0000000000000000013")));

        Assert.Equal(lapse.Keys.Order(), decline.Keys.Order());
        Assert.Equal("01HZL0000000000000000012", lapse["orderId"]);
        Assert.Equal("01HZL0000000000000000013", decline["orderId"]);
        Assert.All(messages, m => Assert.Contains(NotificationEventCatalog.PreferredOfferClosed, m.Body));
    }

    // ── Who is NOT this sweep's business ──

    [Fact]
    public async Task A_Live_Reservation_Is_Left_Alone()
    {
        await EnsureSchemaAsync();
        await SeedAsync(ReservedOrder("01HZL0000000000000000005", lapsesAt: DateTime.UtcNow.AddHours(2)));

        var response = await RunSweepAsync();

        Assert.Equal(0, response.NotifiedCount);
        Assert.Empty(await ReadOutboxKeysAsync());
        Assert.Null((await ReadOrderAsync("01HZL0000000000000000005")).PreferredOfferLapseNotifiedAt);
    }

    /// <summary>
    /// The "nobody came" case is the whole point. A cleaner on the job means the reservation was
    /// honoured or the board picked it up, and the customer already heard about that through
    /// <c>order.cleaner_assigned</c> — telling them the offer closed as well would be AC4's "never
    /// both".
    /// </summary>
    [Fact]
    public async Task An_Order_Somebody_Took_Is_Not_Prompted()
    {
        await EnsureSchemaAsync();
        var order = ReservedOrder("01HZL0000000000000000006", lapsesAt: DateTime.UtcNow.AddMinutes(-1));
        order.SetMaxEmployees(2);
        order.AddAssignedEmployee(OrderEmployee.Create(order, NewCleaner(RivalId)));
        await SeedAsync(order);

        var response = await RunSweepAsync();

        Assert.Equal(0, response.NotifiedCount);
        Assert.Empty(await ReadOutboxKeysAsync());
    }

    /// <summary>
    /// D6.2 — the materializer carries the template's preference into every occurrence 7 days ahead, so
    /// un-suppressed a weekly template whose favourite never answers produces one customer push per
    /// week, forever. Reject where someone can react; degrade where nobody can — the customer did not
    /// initiate this booking and cannot usefully be asked about it at 03:00.
    /// </summary>
    [Fact]
    public async Task A_Recurring_Occurrence_Gets_The_Reservation_But_Not_The_Prompt()
    {
        await EnsureSchemaAsync();
        var order = ReservedOrder(
            "01HZL0000000000000000007",
            lapsesAt: DateTime.UtcNow.AddMinutes(-1),
            recurringTemplateId: "tmpl-weekly");
        await SeedAsync(order);

        var response = await RunSweepAsync();

        Assert.Equal(0, response.NotifiedCount);
        Assert.Empty(await ReadOutboxKeysAsync());
        Assert.Equal(BeneficiaryId, (await ReadOrderAsync("01HZL0000000000000000007")).PreferredEmployeeId);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed)]
    public async Task A_Terminal_Order_Is_Not_Prompted(OrderStatus terminal)
    {
        await EnsureSchemaAsync();
        var order = ReservedOrder("01HZL0000000000000000008", lapsesAt: DateTime.UtcNow.AddMinutes(-1));
        order.AddOrderStatus(OrderStatusTrack.Create(terminal, order));
        await SeedAsync(order);

        var response = await RunSweepAsync();

        Assert.Equal(0, response.NotifiedCount);
        Assert.Empty(await ReadOutboxKeysAsync());
    }

    [Fact]
    public async Task An_Order_That_Never_Carried_A_Reservation_Is_Not_Prompted()
    {
        await EnsureSchemaAsync();
        await SeedAsync(ReservedOrder("01HZL0000000000000000009", lapsesAt: null));

        var response = await RunSweepAsync();

        Assert.Equal(0, response.NotifiedCount);
        Assert.Empty(await ReadOutboxKeysAsync());
    }

    // ── Tenancy ──

    /// <summary>
    /// The commit lives INSIDE the per-tenant loop: rows are stamped from the ambient tenant AT COMMIT
    /// TIME, so one deferred commit would stamp every group with whichever tenant was processed last.
    /// </summary>
    [Fact]
    public async Task Each_Tenant_Groups_Rows_Are_Stamped_With_Their_Own_Tenant()
    {
        await EnsureSchemaAsync();

        var tenanted = ReservedOrder(
            "01HZL0000000000000000010", lapsesAt: DateTime.UtcNow.AddMinutes(-1), userId: "user-tenanted");
        tenanted.TenantId = "tenant-a";
        var legacy = ReservedOrder(
            "01HZL0000000000000000011", lapsesAt: DateTime.UtcNow.AddMinutes(-2), userId: "user-legacy");
        await SeedAsync(tenanted, legacy);

        await RunSweepAsync();

        var dispatches = await ReadOutboxAsync();
        Assert.Equal(
            "tenant-a",
            Assert.Single(dispatches, m => m.MessageKey.Contains("user-tenanted")).TenantId);
        Assert.Null(Assert.Single(dispatches, m => m.MessageKey.Contains("user-legacy")).TenantId);
    }

    // ── Fixture ──

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

    private static Order ReservedOrder(
        string orderId,
        DateTime? lapsesAt,
        string? recurringTemplateId = null,
        string userId = "user-customer")
    {
        var order = Order.Create(
            customerName: "Lapse Customer",
            customerEmail: "lapse@cleansia.test",
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Lapse St 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            preferredEmployeeId: BeneficiaryId,
            recurringTemplateId: recurringTemplateId);
        order.Id = orderId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

        if (lapsesAt is { } deadline)
        {
            order.GrantPreferredHold(
                BeneficiaryId, deadline, deadline.AddHours(-2), BookingPolicy.MaxPreferredOfferRounds);
        }

        return order;
    }

    private static Employee NewCleaner(string employeeId)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test", "Test-password-1!", "Rita", "Rival", UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        return employee;
    }

    private async Task SeedAsync(params Order[] orders)
    {
        await using var ctx = NewContext();
        ctx.Orders.AddRange(orders);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<NotifyLapsedPreferredOffers.Response> RunSweepAsync()
    {
        await using var ctx = NewContext();
        var handler = new NotifyLapsedPreferredOffers.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            _tenantProvider,
            ctx,
            NullLogger<NotifyLapsedPreferredOffers.Handler>.Instance);

        var result = await handler.Handle(new NotifyLapsedPreferredOffers.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task RunDeclineAsync(string orderId)
    {
        await using var ctx = NewContext();
        var accessService = new Mock<IOrderAccessService>();
        accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BeneficiaryId);

        var handler = new DeclinePreferredOffer.Handler(
            new OrderRepository(ctx),
            accessService.Object,
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)));

        Assert.True((await handler.Handle(new DeclinePreferredOffer.Command(orderId), default)).IsSuccess);
        await ctx.CommitAsync(CancellationToken.None);
    }

    private static Dictionary<string, string> ArgsOf(OutboxMessage message) =>
        JsonDocument.Parse(message.Body)
            .RootElement.GetProperty("payload").GetProperty("args")
            .EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString()!);

    private async Task<List<string>> OpenToRivalAsync(DateTime nowUtc)
    {
        await using var ctx = NewContext();
        return await new OrderRepository(ctx)
            .GetQueryable()
            .Where(OrderVisibility.NotHeldFrom(RivalId, nowUtc))
            .Select(o => o.Id)
            .ToListAsync();
    }

    private async Task<Order> ReadOrderAsync(string orderId)
    {
        await using var ctx = NewContext();
        return await ctx.Orders
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .FirstAsync(o => o.Id == orderId);
    }

    private async Task<List<UserNotification>> ReadFeedAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Set<UserNotification>().IgnoreQueryFilters().ToListAsync();
    }

    private async Task<List<OutboxMessage>> ReadOutboxAsync()
    {
        await using var ctx = NewContext();
        return await ctx.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.QueueName == QueueNames.NotificationsDispatch)
            .ToListAsync();
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

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
