using Microsoft.Extensions.Configuration;
using Cleansia.Infra.Common.Configuration;
using System.Globalization;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Constants = Cleansia.Core.AppServices.Common.Constants;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// <c>order.confirmed</c>'s two producers — the Stripe webhook and the customer confirming their own
/// recurring occurrence — establish that the booking is PAID and nothing more: on both, the order only
/// becomes offerable at that moment, so no cleaner has yet seen it. Neither may claim one, and neither
/// may borrow the assignment key.
///
/// <para><b>Neither writes a fulfilment status any more</b> (T-0691, owner ruling 2026-09-08).
/// <c>OrderStatus.Confirmed</c> now means only "a cleaner took this job", so both producers move the
/// MONEY axis and leave the order at <c>New</c>. This class is the reason that change is safe to make
/// in one commit: it already separated "the booking is confirmed" from "a cleaner has it", and asserted
/// the push key never claims a cleaner. Only the status assertions moved.</para>
///
/// <para>The push key keeps its name. <c>order.confirmed</c> now describes a money event with a
/// fulfilment-sounding name, which is untidy — but renaming it costs ten locale files across two mobile
/// platforms plus the feed catalogue, and the customer still needs telling that their payment landed.
/// The copy guards that forbid it claiming a cleaner (<c>PushLocKeyCatalogTests</c>,
/// <c>NotificationTemplatesTest</c>) are unaffected and still true.</para>
/// </summary>
public class OrderConfirmedHonestProducerTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string OrderId = "order-confirmed-1";
    private const string CustomerUserId = "user-customer-1";
    private const string TenantId = "tenant-1";

    private readonly List<string> _sentEventKeys = [];
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public OrderConfirmedHonestProducerTests()
    {
        _notificationProducer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, eventKey, _, _, _, _) => _sentEventKeys.Add(eventKey))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task The_Stripe_Webhook_Confirms_The_Booking_And_Claims_No_Cleaner()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: null);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var result = await CreateWebhookHandler().Handle(
            SettlementCommand("evt_confirmed_1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The money axis moved and the fulfilment axis did NOT. This assertion used to read
        // Confirmed; that it now reads New is the whole of T-0691 at its writer.
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Empty(order.AssignedEmployees);
        Assert.Equal([NotificationEventCatalog.OrderConfirmed], _sentEventKeys);
    }

    [Fact]
    public async Task The_Recurring_Cash_Confirmation_Confirms_The_Booking_And_Claims_No_Cleaner()
    {
        var order = ArrangeOrder(PaymentType.Cash, recurringTemplateId: "tmpl-1");
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(CustomerUserId);

        var result = await new ConfirmRecurringOrder.Handler(
            _orderRepository.Object,
            new Mock<ICreditAccountRepository>().Object,
            new Mock<IUserRepository>().Object,
            session.Object,
            new Mock<Core.Clients.Abstractions.Stripe.IStripeClient>().Object,
            new StripeConfig(new ConfigurationBuilder().Build()),
            _pending.Object,
            _notificationProducer.Object,
            NoPreferredCleanerHold.Resolver,
            NullLogger<ConfirmRecurringOrder.Handler>.Instance)
            .Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Same as the webhook above: money moves, fulfilment does not. The occurrence stays offerable
        // because OrderAvailability admits New with a satisfied money term — before T-0691 the
        // Confirmed append was load-bearing here, since a recurring CASH order at New is refused by
        // the money term.
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Empty(order.AssignedEmployees);
        Assert.Equal([NotificationEventCatalog.OrderConfirmed], _sentEventKeys);
    }

    /// <summary>
    /// The offerability half, asserted here rather than trusted. Both producers leave the order at New,
    /// and New is only offerable because the money term is satisfied — so if either the status write or
    /// the payment write regressed, the job would silently never reach a cleaner. That is the failure
    /// mode this split is most exposed to, and it is invisible in the two facts above.
    /// </summary>
    [Theory]
    [InlineData(PaymentType.Card, null)]
    [InlineData(PaymentType.Cash, "tmpl-1")]
    public void A_Paid_Order_Resting_At_New_Is_Still_Offerable(
        PaymentType paymentType, string? recurringTemplateId)
    {
        Assert.True(OrderAvailability.IsOfferable(
            OrderStatus.New, paymentType, PaymentStatus.Paid, recurringTemplateId));
    }

    private static Order ArrangeOrder(PaymentType paymentType, string? recurringTemplateId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: Core.Domain.Users.Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: CustomerUserId,
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        return order;
    }

    private HandlePaymentNotification.Handler CreateWebhookHandler()
    {
        var stripeConfig = new Mock<IStripeConfig>();
        stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);

        var processedEvents = new Mock<IProcessedStripeEventRepository>();
        processedEvents
            .Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var disputes = new Mock<IDisputeRepository>();
        disputes
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);

        return new HandlePaymentNotification.Handler(
            stripeConfig.Object,
            _orderRepository.Object,
            new Mock<ICreditAccountRepository>().Object,
            disputes.Object,
            processedEvents.Object,
            new Mock<IStripeSubscriptionWebhookHandler>().Object,
            new Mock<ITenantProvider>().Object,
            _pending.Object,
            _notificationProducer.Object,
            NoPreferredCleanerHold.Resolver,
            NullLogger<HandlePaymentNotification.Handler>.Instance);
    }

    private static HandlePaymentNotification.Command SettlementCommand(string eventId)
    {
        var payload = SettlementPayload(eventId);
        return new HandlePaymentNotification.Command(payload, SignPayload(payload));
    }

    private static string SettlementPayload(string eventId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Constants.StripeEventType.CompletedSession}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "cs_test_123",
              "object": "checkout.session",
              "payment_status": "paid",
              "metadata": { "OrderId": "{{OrderId}}" }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    private static string SignPayload(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = EventUtility.ComputeSignature(WebhookSecret, timestamp, payload);
        return $"t={timestamp},v1={signature}";
    }
}
