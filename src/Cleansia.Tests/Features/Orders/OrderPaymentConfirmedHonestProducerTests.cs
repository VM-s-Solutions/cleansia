using Microsoft.Extensions.Configuration;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Infra.Common.Configuration;
using System.Globalization;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Constants = Cleansia.Core.AppServices.Common.Constants;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;
using Cleansia.Tests.Common;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Payment settlement emits the money-axis notification without claiming a cleaner or changing
/// fulfilment status. The recurring cash confirmation moves no money, so it says nothing about payment:
/// the occurrence stays Pending until the cleaner records the cash (owner ruling 2026-09-28). Each
/// producer's own guard prevents replay.
/// </summary>
public class OrderPaymentConfirmedHonestProducerTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string OrderId = "order-confirmed-1";
    private const string CustomerUserId = "user-customer-1";
    private const string TenantId = "tenant-1";

    private readonly List<string> _sentEventKeys = [];
    private readonly Mock<INotificationProducer> _notificationProducer = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public OrderPaymentConfirmedHonestProducerTests()
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
        var replay = await CreateWebhookHandler().Handle(SettlementCommand("evt_confirmed_replay"), CancellationToken.None);
        Assert.True(replay.IsSuccess);
        // The money axis moved and the fulfilment axis did NOT. This assertion used to read
        // Confirmed; that it now reads New is the whole of T-0691 at its writer.
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Empty(order.AssignedEmployees);
        Assert.Equal([NotificationEventCatalog.OrderPaymentConfirmed], _sentEventKeys);
    }

    [Fact]
    public async Task The_Card_Payment_That_Concludes_The_Contract_Sends_Its_Confirmation_Dated_Then()
    {
        var order = ArrangeOrder(PaymentType.Card, recurringTemplateId: null);
        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var before = DateTimeOffset.UtcNow;

        var result = await CreateWebhookHandler().Handle(SettlementCommand("evt_concluded_1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendOrderBookedEmailMessage>>(e =>
                e.TenantId == TenantId && e.Payload.OrderId == OrderId
                && e.Payload.ContractConcludedOn >= before && e.Payload.ContractConcludedOn <= DateTimeOffset.UtcNow),
            MessageKeys.OrderBookedEmail(OrderId)), Times.Once);
    }

    [Fact]
    public async Task The_Recurring_Cash_Confirmation_Confirms_The_Booking_And_Claims_No_Payment_Or_Cleaner()
    {
        var order = ArrangeOrder(PaymentType.Cash, recurringTemplateId: "tmpl-1");
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(CustomerUserId);

        var handler = new ConfirmRecurringOrder.Handler(
            OrderAccessDoubles.Over(_orderRepository, session),
            _orderRepository.Object,
            SavedCards.SavedCardDoubles.Guaranteed(), Mock.Of<IReceivableRepository>(),
            new Mock<ICreditAccountRepository>().Object,
            new Mock<IUserRepository>().Object,
            session.Object,
            Mock.Of<ITenantProvider>(),
            new Mock<Core.Clients.Abstractions.Stripe.IStripeClient>().Object,
            new StripeConfig(new ConfigurationBuilder().Build()),
            new OrderChannelProvider(OrderChannel.Mobile),
            _pending.Object,
            _notificationProducer.Object,
            NoPreferredCleanerHold.Resolver,
            Mock.Of<IAdminNotifier>(),
            new AuditContext(),
            NullLogger<ConfirmRecurringOrder.Handler>.Instance);
        var result = await handler.Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        Assert.True(result.IsSuccess);
        var replay = await handler.Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);
        Assert.True(replay.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderRecurringAlreadyConfirmed, replay.Error!.Message);
        // Neither axis moves. The marker is what keeps the occurrence on the board, and the cleaner
        // records the cash at the door as on any cash order.
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.NotNull(order.CustomerConfirmedAt);
        Assert.False(order.AwaitsCustomerConfirmation);
        Assert.Empty(order.AssignedEmployees);
        Assert.Empty(_sentEventKeys);
        // The booking e-mail, not a receipt: the receipt is issued at completion.
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail, It.IsAny<It.IsAnyType>(), MessageKeys.OrderBookedEmail(OrderId)), Times.Once);
        _pending.Verify(p => p.Enqueue(
            QueueNames.GenerateReceipt, It.IsAny<It.IsAnyType>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// The offerability half, asserted here rather than trusted. Both producers leave the order at New,
    /// and New is only offerable because the money term is satisfied — so if either the status write or
    /// the payment write regressed, the job would silently never reach a cleaner. That is the failure
    /// mode this split is most exposed to, and it is invisible in the two facts above.
    /// </summary>
    [Theory]
    [InlineData(PaymentType.Card, null, false)]
    [InlineData(PaymentType.Cash, "tmpl-1", true)]
    public void The_Order_Each_Producer_Leaves_At_New_Is_Still_Offerable(
        PaymentType paymentType, string? recurringTemplateId, bool confirmedByCustomer)
    {
        var paymentStatus = paymentType == PaymentType.Card ? PaymentStatus.Paid : PaymentStatus.Pending;
        Assert.True(OrderAvailability.IsOfferable(
            OrderStatus.New, paymentType, paymentStatus, recurringTemplateId,
            confirmedByCustomer ? DateTime.UtcNow : null));
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
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
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
            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
            _notificationProducer.Object,
            NoPreferredCleanerHold.Resolver,
            Mock.Of<IAdminNotifier>(),
            Mock.Of<IUserNotificationRepository>(),
            Mock.Of<IStripeClientFactory>(),
            Mock.Of<ITenantRepository>(),
            Mock.Of<ISavedCardRepository>(),
            Mock.Of<IReceivableRepository>(),
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
