using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// CancelOrder routed onto the one refund seam (ADR-0006): the handler no longer issues an inline,
/// un-keyed Stripe refund — it delegates the money call to <see cref="IRefundService"/> with
/// Reason=CustomerCancellation and the amount <c>order.Cancel(...)</c> computes. The confirm-then-record
/// payment-status flip lives in the seam (a Stripe failure reports RefundInitiated=false and the handler
/// never flips PaymentStatus itself), and the ADR-0002 OrderRefunded notification enqueue stays here.
/// </summary>
public class CancelOrderRefundSeamTests
{
    private const string OrderId = "order-cancel-1";
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<ICancellationPolicyResolver> _policyResolver = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public CancelOrderRefundSeamTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _policyResolver
            .Setup(r => r.ResolveForUserAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(
                FreeCancellationHours: 48,
                PartialCancellationHours: 24,
                PartialCancellationFeeRate: 0.25m,
                LastMinuteCancellationFeeRate: 0.5m));
    }

    private CancelOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _refundService.Object,
            _loyaltyService.Object,
            _policyResolver.Object,
            _pending.Object);

    private Order ArrangeCardPaidPendingOrder()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(10),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_cancel");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Pending, order));

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void ArrangeSeamSuccess(decimal confirmedAmount)
    {
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    RefundId: "refund-1",
                    RefundKey: $"refund:{req.OrderId}:cancel",
                    Amount: confirmedAmount,
                    Status: RefundStatus.Succeeded,
                    ResolvedToExisting: false)));
    }

    [Fact]
    public async Task Cancel_WithRefund_CallsSeamOnce_WithCustomerCancellationReason_AndComputedAmount()
    {
        var order = ArrangeCardPaidPendingOrder();
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:cancel", 1000m, RefundStatus.Succeeded, false)));

        var result = await CreateHandler().Handle(
            new CancelOrder.Command(OrderId, "changed my mind"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(OrderId, captured!.OrderId);
        Assert.Equal(RefundReason.CustomerCancellation, captured.Reason);
        Assert.Equal(order.CancellationRefundAmount!.Value, captured.Amount);
        Assert.Null(captured.DisputeId);
    }

    [Fact]
    public async Task Cancel_RefundSuccess_ReportsRefundInitiated_AndEnqueuesOrderRefunded()
    {
        ArrangeCardPaidPendingOrder();
        ArrangeSeamSuccess(confirmedAmount: 1000m);

        var result = await CreateHandler().Handle(
            new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundInitiated);
        _pending.Verify(p => p.Enqueue(
            QueueNames.NotificationsDispatch,
            It.Is<QueueEnvelope<SendPushNotificationMessage>>(e =>
                e.Payload.EventKey == NotificationEventCatalog.OrderRefunded
                && e.Payload.UserId == UserId),
            MessageKeys.Push(UserId, NotificationEventCatalog.OrderRefunded, OrderId)),
            Times.Once);
    }

    [Fact]
    public async Task Cancel_StripeRefundFails_LeavesPaymentStatusUnflipped_ReportsRefundInitiatedFalse_NoNotification()
    {
        var order = ArrangeCardPaidPendingOrder();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(
            new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RefundInitiated);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(order.CancellationRefundAmount!.Value, result.Value.RefundAmount);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(), It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_SeamResolvesToExisting_StillReportsRefundInitiated_AndEnqueuesOnce()
    {
        ArrangeCardPaidPendingOrder();
        _refundService
            .Setup(s => s.IssueRefundAsync(
                It.Is<RefundRequest>(r => r.Reason == RefundReason.CustomerCancellation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:cancel", 1000m, RefundStatus.Succeeded,
                ResolvedToExisting: true)));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(
                It.Is<RefundRequest>(r => r.Reason == RefundReason.CustomerCancellation),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _pending.Verify(p => p.Enqueue(
            QueueNames.NotificationsDispatch,
            It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(),
            MessageKeys.Push(UserId, NotificationEventCatalog.OrderRefunded, OrderId)),
            Times.Once);
    }
}
