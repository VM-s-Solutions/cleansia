using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Features.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// A checkout still open when its order was cancelled can be paid afterwards: a one-off web order the
/// stale-order sweep released, or a recurring occurrence retracted at its cutoff. Recording Paid would
/// receipt the sale and tell the customer the payment went through, for a clean nobody will do. The order
/// stays unpaid and an escalated dispute asks an administrator to refund it.
/// </summary>
public class LateSettlementAfterCancellationTests
{
    private const string WebhookSecret = "whsec_after_cancel";
    private const string OrderId = "order-paid-after-cancel";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<IStripeConfig> _stripeConfig = new();

    public LateSettlementAfterCancellationTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _processedEvents
            .Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
    }

    private HandlePaymentNotification.Handler CreateHandler() => new(
        _stripeConfig.Object,
        _orderRepository.Object,
        Mock.Of<ICreditAccountRepository>(),
        _disputeRepository.Object,
        _processedEvents.Object,
        Mock.Of<IStripeSubscriptionWebhookHandler>(),
        Mock.Of<ITenantProvider>(),
        _pending.Object,
        new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
        _producer.Object,
        NoPreferredCleanerHold.Resolver,
        Mock.Of<IAdminNotifier>(),
        Mock.Of<IUserNotificationRepository>(),
        Mock.Of<IStripeClientFactory>(),
        Mock.Of<ITenantRepository>(),
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private Order ArrangeCancelledOrder(string? recurringTemplateId, string cancellationReason)
    {
        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: "jana@example.test",
            customerPhone: "+420777123456",
            customerAddress: Core.Domain.Users.Address.Create("Hlavni 1", "Praha", "11000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(1),
            paymentType: PaymentType.Card,
            totalPrice: 990m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-after-cancel",
            recurringTemplateId: recurringTemplateId);
        order.Id = OrderId;
        order.TenantId = "tenant-after-cancel";
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        order.Cancel(DateTime.UtcNow.AddMinutes(-5), CancelledBy.System, 0m, 0m, reason: cancellationReason);

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    public static TheoryData<string, string?, string> Cases => new()
    {
        { Constants.StripeEventType.CompletedSession, null, OrderCancellationReasons.PaymentNotCompleted },
        { Constants.StripeEventType.PaymentIntentSucceeded, null, OrderCancellationReasons.PaymentNotCompleted },
        { Constants.StripeEventType.CompletedSession, "tmpl-weekly", OrderCancellationReasons.RecurringNotConfirmed },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_Payment_For_A_Cancelled_Order_Is_Escalated_For_A_Refund_And_Not_Recorded_As_Paid(
        string eventType, string? recurringTemplateId, string cancellationReason)
    {
        var order = ArrangeCancelledOrder(recurringTemplateId, cancellationReason);
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>())).Callback<Dispute>(d => added = d);

        var result = await CreateHandler().Handle(SettlementCommand(eventType), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(DisputeStatus.Escalated, added!.Status);
        Assert.Equal(OrderId, added.OrderId);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        _pending.Verify(
            p => p.Enqueue(It.IsAny<string>(), It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(), It.IsAny<string>()),
            Times.Never);
        _producer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static HandlePaymentNotification.Command SettlementCommand(string eventType)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataObject = eventType == Constants.StripeEventType.CompletedSession
            ? $$"""
              {
                "id": "cs_test_after_cancel",
                "object": "checkout.session",
                "payment_status": "paid",
                "metadata": { "OrderId": "{{OrderId}}" }
              }
              """
            : $$"""
              {
                "id": "pi_test_after_cancel",
                "object": "payment_intent",
                "status": "succeeded",
                "metadata": { "OrderId": "{{OrderId}}" }
              }
              """;
        var payload = $$"""
        {
          "id": "evt_after_cancel",
          "object": "event",
          "api_version": "2024-06-20",
          "type": "{{eventType}}",
          "created": {{created}},
          "livemode": false,
          "pending_webhooks": 0,
          "request": null,
          "data": {
            "object": {{dataObject}},
            "previous_attributes": null
          }
        }
        """;
        var timestamp = created.ToString(CultureInfo.InvariantCulture);
        return new HandlePaymentNotification.Command(
            payload, $"t={timestamp},v1={EventUtility.ComputeSignature(WebhookSecret, timestamp, payload)}");
    }
}
