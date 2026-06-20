using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using Dispute = Cleansia.Core.Domain.Disputes.Dispute;

namespace Cleansia.Tests.Features.Payments;

/// <summary>
/// ADR-0006 D4 (chargeback linkage) — <c>charge.dispute.*</c> webhook wiring in
/// <see cref="HandlePaymentNotification"/>. A real bank chargeback carries a charge + payment_intent
/// but no order metadata, so it resolves to the local <see cref="Order"/> via
/// <see cref="IOrderRepository.GetByStripePaymentIntentIdIgnoringTenantAsync"/> and links to a
/// <see cref="Dispute"/> through the long-dead <see cref="Dispute.LinkStripeDispute"/> producer.
///   - created with no open in-app dispute → create + link + Escalate (AC1);
///   - created with an open in-app dispute → link the existing one, never stack a second (AC2);
///   - same Stripe event id redelivered → the ProcessedStripeEvents gate short-circuits, no second
///     side effect (AC3, S7);
///   - updated/closed (won → Resolved, lost → Closed) → found by StripeDisputeId (the id <c>.created</c>
///     actually wrote), no new dispute (AC4);
///   - charge that resolves to no local order → success, warning-level, nothing written (S6);
///   - invalid signature → rejected, nothing written.
/// The status write is routed through the same transition guard the in-app path obeys: <c>lost</c>/
/// non-terminal targets gate on <see cref="Dispute.CanTransitionTo"/>; <c>won</c> gates on
/// <see cref="Dispute.IsTerminal"/> (Resolve is owned outside the table). A late/out-of-order event
/// that would overwrite a terminal dispute is a no-op success (S6 — no retry-inducing failure). The
/// reflection read is tenant-ignoring (the webhook is anonymous), then re-scopes via SetTenantOverride
/// before the write, so it reflects status onto a non-null-tenant dispute in multi-tenant mode.
/// </summary>
public class HandleChargebackNotificationTests
{
    private const string WebhookSecret = "whsec_test_secret";
    private const string PaymentIntentId = "pi_test_123";
    private const string StripeDisputeId = "dp_test_123";
    private const string OrderId = "order-cb-1";
    private const string TenantId = "tenant-1";

    private readonly Mock<IStripeConfig> _stripeConfig = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IProcessedStripeEventRepository> _processedEvents = new();
    private readonly Mock<IStripeSubscriptionWebhookHandler> _subscriptionHandler = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public HandleChargebackNotificationTests()
    {
        _stripeConfig.SetupGet(c => c.WebhookSecret).Returns(WebhookSecret);
        _processedEvents
            .Setup(r => r.HasProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
        _disputeRepository
            .Setup(r => r.GetByStripeDisputeIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
    }

    private HandlePaymentNotification.Handler CreateHandler() =>
        (HandlePaymentNotification.Handler)Activator.CreateInstance(
            typeof(HandlePaymentNotification.Handler),
            _stripeConfig.Object,
            _orderRepository.Object,
            _disputeRepository.Object,
            _processedEvents.Object,
            _subscriptionHandler.Object,
            _tenantProvider.Object,
            _pending.Object,
            NullLogger<HandlePaymentNotification.Handler>.Instance)!;

    private static Order ArrangeOrder()
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.Id = OrderId;
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.TenantId = TenantId;
        return order;
    }

    private void ArrangeOrderResolvable()
    {
        var order = ArrangeOrder();
        _orderRepository
            .Setup(r => r.GetByStripePaymentIntentIdIgnoringTenantAsync(PaymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
    }

    private static Dispute ArrangeOpenDispute(string? tenantId = null)
    {
        var dispute = new Dispute(
            orderId: OrderId,
            userId: "user-1",
            reason: DisputeReason.QualityIssue,
            description: "Existing in-app dispute opened by the customer.",
            createdBy: "user-1");
        if (tenantId is not null)
        {
            dispute.TenantId = tenantId;
        }
        return dispute;
    }

    private HandlePaymentNotification.Command ChargebackCommand(
        string eventType, string disputeStatus, string eventId)
    {
        var payload = ChargebackPayload(eventType, disputeStatus, eventId);
        var signature = SignPayload(payload);
        return new HandlePaymentNotification.Command(payload, signature);
    }

    private static string ChargebackPayload(string eventType, string disputeStatus, string eventId)
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
            "object": {
              "id": "{{StripeDisputeId}}",
              "object": "dispute",
              "amount": 1000,
              "charge": "ch_test_123",
              "created": {{created}},
              "currency": "czk",
              "is_charge_refundable": false,
              "livemode": false,
              "payment_intent": "{{PaymentIntentId}}",
              "reason": "fraudulent",
              "status": "{{disputeStatus}}"
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

    [Fact]
    public async Task ChargebackCreated_NoOpenDispute_CreatesLinksAndEscalates()
    {
        ArrangeOrderResolvable();
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>()))
            .Callback<Dispute>(d => added = d);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Once);
        Assert.NotNull(added);
        Assert.Equal(StripeDisputeId, added!.StripeDisputeId);
        Assert.Equal(DisputeStatus.Escalated, added.Status);
        Assert.Equal(OrderId, added.OrderId);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.Once);
    }

    // The created dispute's escalation is funnelled through the CanTransitionTo guard
    // (dispute.UpdateStatus(Escalated)) rather than a bare dispute.Escalate. A fresh dispute is Pending
    // and Pending→Escalated is a legal edge, so it is persisted with Escalated status and the actor is
    // stamped through the guard — proving the legal path is unchanged while the write now goes through
    // the guard (an illegal start state would be rejected before the Add, never force the edge).
    [Fact]
    public async Task ChargebackCreated_EscalatesThroughTransitionGuard_AndStampsActor()
    {
        ArrangeOrderResolvable();
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>()))
            .Callback<Dispute>(d => added = d);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_guard"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(DisputeStatus.Escalated, added!.Status);
        Assert.True(added.CanTransitionTo(DisputeStatus.Closed));
        Assert.Equal("stripe-webhook", added.UpdatedBy);
    }

    [Fact]
    public async Task ChargebackCreated_ExistingOpenDispute_LinksWithoutStacking()
    {
        ArrangeOrderResolvable();
        var existing = ArrangeOpenDispute();
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_2"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        Assert.Equal(StripeDisputeId, existing.StripeDisputeId);
    }

    [Fact]
    public async Task ChargebackCreated_Redelivered_ShortCircuitsWithNoSecondSideEffect()
    {
        ArrangeOrderResolvable();
        _processedEvents
            .Setup(r => r.HasProcessedAsync("evt_3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_3"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        _processedEvents.Verify(r => r.Add(It.IsAny<Cleansia.Core.Domain.Payments.ProcessedStripeEvent>()), Times.Never);
        _orderRepository.Verify(
            r => r.GetByStripePaymentIntentIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ChargebackSequence_CreatedThenUpdatedThenClosedLost_ReflectsFinalStatusWithoutNewDispute()
    {
        ArrangeOrderResolvable();
        var dispute = ArrangeOpenDispute(tenantId: TenantId);
        // The reflection read resolves ONLY by the StripeDisputeId that .created actually wrote — if
        // .created never linked the dispute (StripeDisputeId stays null) the updated/closed reads return
        // null and the sequence cannot reflect a final status. This keeps the test honest: it fails if
        // the .created path stops calling LinkStripeDispute.
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        _disputeRepository
            .Setup(r => r.GetByStripeDisputeIdIgnoringTenantAsync(StripeDisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => dispute.StripeDisputeId == StripeDisputeId ? dispute : null);
        var handler = CreateHandler();

        var created = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_4a"),
            CancellationToken.None);

        // .created MUST have linked the id before updated/closed are processed.
        Assert.Equal(StripeDisputeId, dispute.StripeDisputeId);

        var updated = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeUpdated, "under_review", "evt_4b"),
            CancellationToken.None);
        var closed = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeClosed, "lost", "evt_4c"),
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(updated.IsSuccess);
        Assert.True(closed.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        Assert.Equal(DisputeStatus.Closed, dispute.Status);
        // The reflection read is tenant-ignoring and re-scopes the write to the dispute's tenant.
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.AtLeastOnce);
    }

    // Regression for the multi-tenant blocker: a non-null-tenant dispute is read tenant-ignoring (the
    // webhook is anonymous, so a tenant-scoped read would return null and never reflect). This fails
    // against a GetByStripeDisputeIdAsync (tenant-scoped) read.
    [Fact]
    public async Task ChargebackClosed_Lost_NonNullTenant_ReflectsViaTenantIgnoringReadAndOverride()
    {
        ArrangeOrderResolvable();
        var dispute = ArrangeOpenDispute(tenantId: TenantId);
        dispute.LinkStripeDispute(StripeDisputeId, "stripe-webhook");
        dispute.Escalate("stripe-webhook");
        _disputeRepository
            .Setup(r => r.GetByStripeDisputeIdIgnoringTenantAsync(StripeDisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeClosed, "lost", "evt_mt"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeStatus.Closed, dispute.Status);
        _tenantProvider.Verify(t => t.SetTenantOverride(TenantId), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ChargebackClosed_Won_ResolvesDispute()
    {
        ArrangeOrderResolvable();
        var dispute = ArrangeOpenDispute();
        _disputeRepository
            .Setup(r => r.GetByStripeDisputeIdIgnoringTenantAsync(StripeDisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeClosed, "won", "evt_5"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
    }

    // Terminal-state guard (Decision A): a late/out-of-order "won" arriving on an already-Closed dispute
    // must NOT force the illegal Closed→Resolved overwrite. It is a benign no-op success — status stays
    // Closed, nothing throws.
    [Fact]
    public async Task ChargebackClosed_Won_OnAlreadyClosedDispute_IsNoOpAndLeavesStatusUnchanged()
    {
        ArrangeOrderResolvable();
        var dispute = ArrangeOpenDispute(tenantId: TenantId);
        dispute.LinkStripeDispute(StripeDisputeId, "stripe-webhook");
        dispute.Escalate("stripe-webhook");
        dispute.Close("stripe-webhook"); // a prior "lost" already closed it
        _disputeRepository
            .Setup(r => r.GetByStripeDisputeIdIgnoringTenantAsync(StripeDisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeClosed, "won", "evt_late_won"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeStatus.Closed, dispute.Status);
    }

    [Fact]
    public async Task Chargeback_UncorrelatedCharge_ReturnsSuccessAndWritesNothing()
    {
        _orderRepository
            .Setup(r => r.GetByStripePaymentIntentIdIgnoringTenantAsync(PaymentIntentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(
            ChargebackCommand(Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_6"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        _tenantProvider.Verify(t => t.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Chargeback_InvalidSignature_IsRejectedAndWritesNothing()
    {
        var payload = ChargebackPayload(
            Constants.StripeEventType.ChargeDisputeCreated, "needs_response", "evt_7");
        var command = new HandlePaymentNotification.Command(payload, "t=1,v1=deadbeef");
        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
        _processedEvents.Verify(
            r => r.Add(It.IsAny<Cleansia.Core.Domain.Payments.ProcessedStripeEvent>()), Times.Never);
    }
}
