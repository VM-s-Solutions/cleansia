using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.LiveActivities;
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
using Moq;

namespace Cleansia.Tests.Features.LiveActivities;

/// <summary>
/// LA-2 — the sibling live-activity producer seam (ADR-0029 D2). Every order transition that carries
/// an activity event enqueues exactly one <see cref="SendLiveActivityUpdateMessage"/> on the
/// <c>live-activity-dispatch</c> queue — BUT only when the order's user holds a registered token, so an
/// order with no iOS activity produces nothing. The gate lives in the producer, not the handlers.
/// </summary>
public class LiveActivityProducerTests
{
    private const string UserId = "user-la-1";
    private const string OrderId = "order-la-1";

    private readonly Mock<ILiveActivityTokenRepository> _tokenRepository = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();

    private LiveActivityProducer CreateProducer() =>
        new(_tokenRepository.Object, _pendingDispatch.Object);

    private static Order BuildOrder(OrderStatus status, out OrderStatusTrack transition, string? userId = UserId)
    {
        var address = Address.Create("123 Main St", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "test@example.com",
            customerPhone: "+420000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.SpecifyKind(new DateTime(2026, 7, 20, 9, 0, 0), DateTimeKind.Utc),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = OrderId;
        order.UpdateEstimatedTime(120);

        transition = OrderStatusTrack.Create(status, order);
        order.AddOrderStatus(transition);
        return order;
    }

    [Fact]
    public async Task Enqueues_One_Message_On_A_Transition_When_A_Token_Is_Registered()
    {
        _tokenRepository
            .Setup(r => r.HasTokensForOrderAsync(UserId, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var order = BuildOrder(OrderStatus.OnTheWay, out var transition);

        await CreateProducer().NotifyOrderTransitionAsync(order, LiveActivityEventKeys.Start, transition, CancellationToken.None);

        _pendingDispatch.Verify(
            p => p.Enqueue(
                QueueNames.LiveActivityDispatch,
                It.IsAny<QueueEnvelope<SendLiveActivityUpdateMessage>>(),
                MessageKeys.LiveActivity(OrderId, LiveActivityEventKeys.Start, transition.Sequence)),
            Times.Once);
    }

    [Theory]
    [InlineData(OrderStatus.OnTheWay, LiveActivityEventKeys.Start)]
    [InlineData(OrderStatus.InProgress, LiveActivityEventKeys.Update)]
    [InlineData(OrderStatus.Completed, LiveActivityEventKeys.End)]
    [InlineData(OrderStatus.Cancelled, LiveActivityEventKeys.End)]
    public async Task Enqueues_With_The_Event_Key_For_Each_Driven_Transition(OrderStatus status, string eventKey)
    {
        _tokenRepository
            .Setup(r => r.HasTokensForOrderAsync(UserId, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var order = BuildOrder(status, out var transition);

        await CreateProducer().NotifyOrderTransitionAsync(order, eventKey, transition, CancellationToken.None);

        _pendingDispatch.Verify(
            p => p.Enqueue(
                QueueNames.LiveActivityDispatch,
                It.Is<QueueEnvelope<SendLiveActivityUpdateMessage>>(e => e.Payload.EventKey == eventKey),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Enqueues_For_A_User_Whose_Only_Token_Is_A_Push_To_Start_Token()
    {
        // The gate reads the REAL HasTokensForOrderAsync LINQ (OrderId == orderId OR OrderId == null): a
        // lone push-to-start token (OrderId == null, NO per-order token) satisfies it, so a qualifying
        // transition still enqueues. Runs against an actual DbContext to pin the OrderId == null branch.
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(connection).Options;
        await using var ctx = new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new NullTenantProvider());
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Language.Create("en", "English")); // the User's PreferredLanguageCode FK target
        var user = User.CreateWithPassword("owner@cleansia.test", "Passw0rd!", "Owner", "User");
        user.Id = UserId;
        ctx.Add(user);
        ctx.Add(LiveActivityToken.Create(UserId, "DEV-1", orderId: null, "PTS-TOKEN", tenantId: null));
        await ctx.CommitAsync(CancellationToken.None);

        var producer = new LiveActivityProducer(new LiveActivityTokenRepository(ctx), _pendingDispatch.Object);
        var order = BuildOrder(OrderStatus.OnTheWay, out var transition);

        await producer.NotifyOrderTransitionAsync(order, LiveActivityEventKeys.Start, transition, CancellationToken.None);

        _pendingDispatch.Verify(
            p => p.Enqueue(
                QueueNames.LiveActivityDispatch,
                It.IsAny<QueueEnvelope<SendLiveActivityUpdateMessage>>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Skips_Entirely_When_The_Order_Has_No_Registered_Token()
    {
        _tokenRepository
            .Setup(r => r.HasTokensForOrderAsync(UserId, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var order = BuildOrder(OrderStatus.InProgress, out var transition);

        await CreateProducer().NotifyOrderTransitionAsync(order, LiveActivityEventKeys.Update, transition, CancellationToken.None);

        _pendingDispatch.Verify(
            p => p.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<SendLiveActivityUpdateMessage>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Guest_Order_Without_A_User_Never_Touches_The_Token_Repository_Or_Enqueues()
    {
        var order = BuildOrder(OrderStatus.OnTheWay, out var transition, userId: null);

        await CreateProducer().NotifyOrderTransitionAsync(order, LiveActivityEventKeys.Start, transition, CancellationToken.None);

        _tokenRepository.Verify(
            r => r.HasTokensForOrderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _pendingDispatch.Verify(
            p => p.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<SendLiveActivityUpdateMessage>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task The_Enqueued_Message_Carries_The_Schedule_And_Transition_Timestamp()
    {
        _tokenRepository
            .Setup(r => r.HasTokensForOrderAsync(UserId, OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var order = BuildOrder(OrderStatus.OnTheWay, out var transition);

        QueueEnvelope<SendLiveActivityUpdateMessage>? captured = null;
        _pendingDispatch
            .Setup(p => p.Enqueue(
                It.IsAny<string>(),
                It.IsAny<QueueEnvelope<SendLiveActivityUpdateMessage>>(),
                It.IsAny<string>()))
            .Callback<string, QueueEnvelope<SendLiveActivityUpdateMessage>, string>((_, e, _) => captured = e);

        await CreateProducer().NotifyOrderTransitionAsync(order, LiveActivityEventKeys.Start, transition, CancellationToken.None);

        Assert.NotNull(captured);
        var message = captured!.Payload;
        Assert.Equal(UserId, message.UserId);
        Assert.Equal(OrderId, message.OrderId);
        Assert.Equal(order.DisplayOrderNumber, message.OrderNumber);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 9, 0, 0, TimeSpan.Zero), message.ScheduledStart);
        Assert.Equal(new DateTimeOffset(2026, 7, 20, 11, 0, 0, TimeSpan.Zero), message.ScheduledEnd);
        Assert.Equal(transition.CreatedOn, message.TransitionAtUtc);
    }

    // S6: the message wire shape is the routing+display allowlist and NOTHING that could leak PII to a
    // lock screen — no customer/cleaner name, address, free text, or contact field.
    [Fact]
    public void The_Message_Exposes_Only_The_Allowlisted_Fields()
    {
        var actual = typeof(SendLiveActivityUpdateMessage)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var allowed = new[]
        {
            "EventKey", "OrderId", "OrderNumber", "ScheduledEnd", "ScheduledStart",
            "TenantId", "TransitionAtUtc", "UserId",
        };

        Assert.Equal(allowed, actual);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
