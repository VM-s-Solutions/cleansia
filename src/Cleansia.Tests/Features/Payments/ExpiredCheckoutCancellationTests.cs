using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// A web checkout Stripe expired is the same abandoned checkout the stale-order sweep releases, and is
/// now cancelled the same way: who, when and why on the order, the guest e-mailed — and an order
/// something else already cancelled is left alone rather than cancelled a second time.
/// </summary>
public class ExpiredCheckoutCancellationTests
{
    private const string OrderId = "order-expired-1";
    private const string WebhookSecret = "whsec_expired_secret";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _guestTokens = new();
    private readonly List<(string Queue, string Key)> _enqueued = [];

    public ExpiredCheckoutCancellationTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _processedEvents.Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _guestTokens.Setup(r => r.GetLiveForOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuestOrderAccessToken>());
    }

    private HandlePaymentNotification.Handler Handler() => new(
        _stripeConfig.Object,
        _orders.Object,
        Mock.Of<ICreditAccountRepository>(),
        Mock.Of<IDisputeRepository>(),
        _processedEvents.Object,
        Mock.Of<IStripeSubscriptionWebhookHandler>(),
        Mock.Of<ITenantProvider>(),
        new RecordingDispatch(_enqueued),
        new GuestOrderAccessTokenIssuer(_guestTokens.Object),
        _producer.Object,
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        Mock.Of<IUserNotificationRepository>(),
        Mock.Of<IStripeClientFactory>(),
        Mock.Of<ITenantRepository>(),
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private Order ArrangeOrder(string? userId = null, OrderStatus status = OrderStatus.New, string? recurringTemplateId = null)
    {
        var order = Order.Create(
            customerName: "Abandoned",
            customerEmail: "abandoned@example.test",
            customerPhone: "+420111444777",
            customerAddress: Cleansia.Core.Domain.Users.Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: userId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.TenantId = "tenant-a";
        var stamp = DateTimeOffset.UtcNow.AddHours(-2);
        foreach (var track in status == OrderStatus.New ? [OrderStatus.New] : new[] { OrderStatus.New, status })
        {
            var entry = OrderStatusTrack.Create(track, order);
            entry.Created("test", stamp);
            order.AddOrderStatus(entry);
            stamp = stamp.AddMinutes(1);
        }

        _orders.Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private Task<Cleansia.Infra.Common.Validations.BusinessResult> ExpireAsync()
    {
        var payload = ExpiredSessionPayload();
        return Handler().Handle(new HandlePaymentNotification.Command(payload, Sign(payload)), CancellationToken.None);
    }

    [Fact]
    public async Task An_Expired_Checkout_Is_Cancelled_With_Who_When_And_Why()
    {
        var order = ArrangeOrder();

        var result = await ExpireAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Failed, order.PaymentStatus);
        Assert.NotNull(order.CancelledAt);
        Assert.Equal(CancelledBy.System, order.CancelledBy);
        Assert.Equal(OrderCancellationReasons.PaymentNotCompleted, order.CancellationReason);
        Assert.Equal(0m, order.CancellationRefundAmount);
    }

    [Fact]
    public async Task A_Guest_Is_Emailed_That_The_Booking_Was_Released()
    {
        ArrangeOrder();

        await ExpireAsync();

        Assert.Contains((QueueNames.SendEmail, MessageKeys.GuestOrderCancelledEmail(OrderId)), _enqueued);
    }

    /// <summary>
    /// Cancelled before the session expired — by the customer, or by the stale-order sweep. The order
    /// keeps its one Cancelled track, its own who and why, and nobody is told a second time.
    /// </summary>
    [Fact]
    public async Task An_Order_Already_Cancelled_Is_Left_Alone()
    {
        var order = ArrangeOrder(userId: "user-expired", status: OrderStatus.Cancelled);
        order.Cancel(DateTime.UtcNow.AddMinutes(-30), CancelledBy.Customer, 0m, 1000m, reason: null);

        var result = await ExpireAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(order.OrderStatusHistory, t => t.Status == OrderStatus.Cancelled);
        Assert.Equal(CancelledBy.Customer, order.CancelledBy);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_enqueued);
    }

    /// <summary>
    /// A recurring occurrence's web checkout did not create the occurrence, so its expiry does not cancel
    /// it: the occurrence waits for the recurring cutoff, and only the session is forgotten, so the
    /// customer may start another on either channel.
    /// </summary>
    [Fact]
    public async Task A_Recurring_Occurrences_Expired_Checkout_Leaves_It_Open_And_Forgets_The_Session()
    {
        var order = ArrangeOrder(userId: "user-recurring", recurringTemplateId: "tmpl-weekly");
        order.AssignStripeSessionId("cs_test_expired");

        var result = await ExpireAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.CancelledAt);
        Assert.Equal(string.Empty, order.StripeSessionId);
        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(_enqueued);
    }

    /// <summary>An older session closing late does not forget the one the occurrence now records.</summary>
    [Fact]
    public async Task A_Recurring_Occurrence_Keeps_The_Session_It_Records_When_An_Older_One_Expires()
    {
        var order = ArrangeOrder(userId: "user-recurring", recurringTemplateId: "tmpl-weekly");
        order.AssignStripeSessionId("cs_test_newer");

        var result = await ExpireAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("cs_test_newer", order.StripeSessionId);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
    }

    private static string ExpiredSessionPayload()
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "evt_expired_1",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Constants.StripeEventType.ExpiredSession}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "cs_test_expired",
              "object": "checkout.session",
              "payment_status": "unpaid",
              "metadata": { "OrderId": "{{OrderId}}" }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    private sealed class RecordingDispatch(List<(string Queue, string Key)> enqueued) : IPendingDispatch
    {
        public void Enqueue<T>(string queueName, T message, string messageKey) => enqueued.Add((queueName, messageKey));

        public IReadOnlyList<PendingMessage> Drain() => [];
    }

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return $"t={timestamp},v1={EventUtility.ComputeSignature(WebhookSecret, timestamp, payload)}";
    }
}
