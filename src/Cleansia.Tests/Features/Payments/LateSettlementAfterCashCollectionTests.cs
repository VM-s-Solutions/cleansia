using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
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
/// The late-webhook hazard created by letting a cleaner settle a CARD order in cash: Stripe's
/// <c>checkout.session.completed</c> / <c>payment_intent.succeeded</c> can still land afterwards, which
/// means the customer paid twice. The webhook must NOT silently flip the order to Paid and must NOT
/// auto-refund (a human decides that) — it logs at Error and raises an ESCALATED dispute so an
/// administrator reconciles it. An order that was not settled in cash keeps the untouched happy path.
/// </summary>
public class LateSettlementAfterCashCollectionTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string OrderId = "order-late-1";
    private const string TenantId = "tenant-1";
    private const string EmployeeId = "emp-1";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IStripeSubscriptionWebhookHandler> _subscriptionHandler = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _producer = new();

    public LateSettlementAfterCashCollectionTests()
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
        _disputeRepository.Object,
        _processedEvents.Object,
        _subscriptionHandler.Object,
        _tenantProvider.Object,
        _pending.Object,
        _producer.Object,
        NoPreferredCleanerHold.Resolver,
        NullLogger<HandlePaymentNotification.Handler>.Instance);

    private Order ArrangeOrder(bool collectedInCash)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        order.Id = OrderId;
        order.TenantId = TenantId;

        if (collectedInCash)
        {
            order.MarkCashCollected(EmployeeId);
        }

        _orderRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    [Theory]
    [InlineData(Constants.StripeEventType.CompletedSession)]
    [InlineData(Constants.StripeEventType.PaymentIntentSucceeded)]
    public async Task Late_Settlement_On_Cash_Collected_Order_Raises_Escalated_Dispute_And_Does_Not_Refund(
        string eventType)
    {
        var order = ArrangeOrder(collectedInCash: true);
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>())).Callback<Dispute>(d => added = d);

        var result = await CreateHandler().Handle(
            SettlementCommand(eventType, "evt_late_1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(DisputeStatus.Escalated, added!.Status);
        Assert.Equal(OrderId, added.OrderId);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.NotNull(order.CashCollectedAt);
        // No second receipt, no "order confirmed" push, and above all no automatic refund.
        _pending.Verify(
            p => p.Enqueue(
                It.IsAny<string>(),
                It.IsAny<QueueEnvelope<GenerateReceiptMessage>>(),
                It.IsAny<string>()),
            Times.Never);
        _producer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Late_Settlement_With_An_Open_Dispute_Escalates_It_Instead_Of_Stacking_A_Second()
    {
        ArrangeOrder(collectedInCash: true);
        var existing = new Dispute(
            orderId: OrderId,
            userId: "user-1",
            reason: DisputeReason.QualityIssue,
            description: "Existing in-app dispute opened by the customer.",
            createdBy: "user-1");
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(
            SettlementCommand(Constants.StripeEventType.CompletedSession, "evt_late_2"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        Assert.Equal(DisputeStatus.Escalated, existing.Status);
    }

    [Fact]
    public async Task Normal_Late_Settlement_Without_Cash_Collection_Still_Confirms_The_Order()
    {
        var order = ArrangeOrder(collectedInCash: false);

        var result = await CreateHandler().Handle(
            SettlementCommand(Constants.StripeEventType.CompletedSession, "evt_normal"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }

    private HandlePaymentNotification.Command SettlementCommand(string eventType, string eventId)
    {
        var payload = SettlementPayload(eventType, eventId);
        return new HandlePaymentNotification.Command(payload, SignPayload(payload));
    }

    private static string SettlementPayload(string eventType, string eventId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataObject = eventType == Constants.StripeEventType.CompletedSession
            ? $$"""
              {
                "id": "cs_test_123",
                "object": "checkout.session",
                "payment_status": "paid",
                "metadata": { "OrderId": "{{OrderId}}" }
              }
              """
            : $$"""
              {
                "id": "pi_test_123",
                "object": "payment_intent",
                "status": "succeeded",
                "metadata": { "OrderId": "{{OrderId}}" }
              }
              """;

        return $$"""
        {
          "id": "{{eventId}}",
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
    }

    private static string SignPayload(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = EventUtility.ComputeSignature(WebhookSecret, timestamp, payload);
        return $"t={timestamp},v1={signature}";
    }
}
