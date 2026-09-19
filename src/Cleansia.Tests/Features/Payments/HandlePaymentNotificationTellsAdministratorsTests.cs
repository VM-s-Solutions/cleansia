using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// The Stripe webhook tells the ORDER's company at three of its arms, each once. A settled session
/// makes a card order offerable, so it raises the new-order event — after the terminal-state
/// short-circuit, so a redelivery raises nothing and a settled order raises nothing again. A declined
/// PaymentIntent raises the payment-failed event ONCE PER ORDER: Stripe fires it per attempt, so the
/// arm reads the feed first and a second decline on an order the company was already told about
/// raises nothing and still succeeds; a decline that lands after the order was paid or cancelled
/// raises nothing without reading the feed. A chargeback raises its event naming the dispute the money is
/// now attached to — the customer's open one when there is one, else the chargeback's own — with the
/// Stripe dispute id as the subject and the disputed amount in the order's currency.
/// </summary>
public sealed class HandlePaymentNotificationTellsAdministratorsTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string OrderId = "order-webhook-tells-1";
    private const string PaymentIntentId = "pi_tells_123";
    private const string StripeDisputeId = "dp_tells_123";
    private const string TenantId = "company-of-the-order";
    private const string CountryId = "country-cz-webhook-tells";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IUserNotificationRepository> _userNotifications = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private readonly List<AdminEvent> _raised = [];

    public HandlePaymentNotificationTellsAdministratorsTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _processedEvents
            .Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
        _userNotifications
            .Setup(r => r.AnyForEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _adminNotifier
            .Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => _raised.Add(e))
            .Returns(Task.CompletedTask);
    }

    private HandlePaymentNotification.Handler Handler() => new(
        _stripeConfig.Object,
        _orderRepository.Object,
        new Mock<ICreditAccountRepository>().Object,
        _disputeRepository.Object,
        _processedEvents.Object,
        new Mock<IStripeSubscriptionWebhookHandler>().Object,
        new Mock<ITenantProvider>().Object,
        new Mock<IPendingDispatch>().Object,
        new Mock<INotificationProducer>().Object,
        NoPreferredCleanerHold.Resolver,
        _adminNotifier.Object,
        _userNotifications.Object,
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private Order ArrangeOrder(PaymentStatus paymentStatus = PaymentStatus.Pending)
    {
        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "jana.novakova@example.com",
            customerPhone: "+420777123456",
            customerAddress: Core.Domain.Users.Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: "currency-czk",
            paymentStatus: paymentStatus,
            userId: "user-customer-tells");
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _orderRepository
            .Setup(r => r.GetByStripePaymentIntentIdIgnoringTenantAsync(PaymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    [Fact]
    public async Task A_Settled_Session_Tells_The_Orders_Company_Of_The_New_Order_With_The_Declared_Args()
    {
        var order = ArrangeOrder();

        var result = await Handler().Handle(Command(SessionPayload("evt_tells_paid")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.OrderNew, raised.Key);
        Assert.Equal(TenantId, raised.TenantId);
        Assert.Equal(OrderId, raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.OrderNew).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal("1500 Kč", raised.Args["amount"]);
        Assert.Equal(nameof(PaymentType.Card), raised.Args["paymentType"]);
        Assert.Equal(CountryId, raised.Args["countryId"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
    }

    [Fact]
    public async Task A_Redelivered_Session_Tells_Nobody_Twice()
    {
        ArrangeOrder();
        var handler = Handler();

        await handler.Handle(Command(SessionPayload("evt_tells_first")), CancellationToken.None);
        _processedEvents
            .Setup(r => r.HasProcessedAsync("evt_tells_first", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var redelivered = await handler.Handle(Command(SessionPayload("evt_tells_first")), CancellationToken.None);
        var later = await handler.Handle(Command(SessionPayload("evt_tells_second")), CancellationToken.None);

        Assert.True(redelivered.IsSuccess);
        Assert.True(later.IsSuccess);
        Assert.Single(_raised);
    }

    [Fact]
    public async Task The_First_Decline_On_An_Order_Tells_The_Company_And_Names_The_Order()
    {
        var order = ArrangeOrder();

        var result = await Handler().Handle(Command(IntentFailedPayload("evt_tells_declined")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.PaymentFailed, raised.Key);
        Assert.Equal(TenantId, raised.TenantId);
        Assert.Equal(OrderId, raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.PaymentFailed).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
        _userNotifications.Verify(
            r => r.AnyForEventAsync(TenantId, AdminNotificationEventCatalog.PaymentFailed, "orderId", OrderId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Second_Decline_On_An_Order_The_Company_Was_Told_About_Tells_Nobody_And_Still_Succeeds()
    {
        ArrangeOrder();
        _userNotifications
            .Setup(r => r.AnyForEventAsync(TenantId, AdminNotificationEventCatalog.PaymentFailed, "orderId", OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(Command(IntentFailedPayload("evt_tells_declined_again")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Decline_Delivered_After_The_Order_Was_Paid_Tells_Nobody_And_Reads_No_Feed()
    {
        ArrangeOrder(PaymentStatus.Paid);

        var result = await Handler().Handle(Command(IntentFailedPayload("evt_tells_declined_late")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_raised);
        _userNotifications.Verify(
            r => r.AnyForEventAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Decline_Delivered_After_The_Customer_Cancelled_The_Unpaid_Order_Tells_Nobody()
    {
        var order = ArrangeOrder();
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

        var result = await Handler().Handle(Command(IntentFailedPayload("evt_tells_declined_dead")), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Empty(_raised);
    }

    [Fact]
    public async Task A_Chargeback_With_No_Open_Dispute_Names_The_Chargebacks_Own_Dispute_And_The_Disputed_Amount()
    {
        var order = ArrangeOrder(PaymentStatus.Paid);
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>())).Callback<Dispute>(d => added = d);

        var result = await Handler().Handle(
            Command(ChargebackPayload("evt_tells_chargeback", amountMinor: 123450)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.DisputeChargeback, raised.Key);
        Assert.Equal(TenantId, raised.TenantId);
        Assert.Equal(StripeDisputeId, raised.Subject);
        var declared = AdminEventCatalog.Find(AdminNotificationEventCatalog.DisputeChargeback).EmailArgOrder;
        Assert.Equal(declared.OrderBy(a => a), raised.Args.Keys.OrderBy(a => a));
        Assert.Equal(added!.Id, raised.Args["disputeId"]);
        Assert.Equal(order.DisplayOrderNumber, raised.Args["orderNumber"]);
        Assert.Equal("1234.5 Kč", raised.Args["amount"]);
        Assert.Equal(OrderId, raised.Args["orderId"]);
    }

    [Fact]
    public async Task A_Chargeback_On_An_Order_With_An_Open_Dispute_Names_That_Dispute()
    {
        ArrangeOrder(PaymentStatus.Paid);
        var existing = new Dispute(OrderId, "user-customer-tells", DisputeReason.QualityIssue, "The floor was not mopped.", "user-customer-tells");
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await Handler().Handle(
            Command(ChargebackPayload("evt_tells_chargeback_linked", amountMinor: 150000)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        var raised = Assert.Single(_raised);
        Assert.Equal(AdminNotificationEventCatalog.DisputeChargeback, raised.Key);
        Assert.Equal(existing.Id, raised.Args["disputeId"]);
        Assert.Equal("1500 Kč", raised.Args["amount"]);
        Assert.DoesNotContain("floor", raised.Args.Values.Aggregate(string.Empty, (a, b) => a + b));
    }

    [Fact]
    public async Task A_Redelivered_Chargeback_Tells_Nobody_Twice()
    {
        ArrangeOrder(PaymentStatus.Paid);
        _processedEvents
            .Setup(r => r.HasProcessedAsync("evt_tells_chargeback_redelivered", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await Handler().Handle(
            Command(ChargebackPayload("evt_tells_chargeback_redelivered", amountMinor: 150000)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_raised);
    }

    private static HandlePaymentNotification.Command Command(string payload) =>
        new(payload, SignPayload(payload));

    private static string SessionPayload(string eventId)
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
              "id": "cs_tells_123",
              "object": "checkout.session",
              "payment_status": "paid",
              "metadata": { "OrderId": "{{OrderId}}" }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    private static string IntentFailedPayload(string eventId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Constants.StripeEventType.PaymentIntentPaymentFailed}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "{{PaymentIntentId}}",
              "object": "payment_intent",
              "amount": 150000,
              "currency": "czk",
              "status": "requires_payment_method",
              "metadata": { "OrderId": "{{OrderId}}" }
            },
            "previous_attributes": null
          }
        }
        """;
    }

    private static string ChargebackPayload(string eventId, long amountMinor)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{Constants.StripeEventType.ChargeDisputeCreated}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {
              "id": "{{StripeDisputeId}}",
              "object": "dispute",
              "amount": {{amountMinor}},
              "charge": "ch_tells_123",
              "created": {{created}},
              "currency": "czk",
              "is_charge_refundable": false,
              "livemode": false,
              "payment_intent": "{{PaymentIntentId}}",
              "reason": "fraudulent",
              "status": "needs_response"
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
