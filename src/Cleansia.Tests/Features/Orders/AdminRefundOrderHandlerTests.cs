using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// AC6 — the admin refund-only command. The Stripe refund is issued ONLY through
/// <see cref="IRefundService"/> with the deterministic full-refund key; the order's LIFECYCLE STATUS
/// is unchanged (it stays Confirmed) while <c>PaymentStatus</c> transitions to
/// Refunded / PartiallyRefunded per the seam. A retry collapses on the same key — no second Stripe
/// refund is issued.
/// </summary>
public class AdminRefundOrderHandlerTests
{
    private const string OrderId = "order-admin-refund-1";
    private const string AdminUserId = "admin-user";
    private const string OwnerUserId = "owner-user";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public AdminRefundOrderHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminUserId);
    }

    private readonly AuditContext _auditContext = new();

    private AdminRefundOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _refundRepository.Object,
            _refundService.Object,
            _session.Object,
            _pending.Object,
            _auditContext);

    private Order ArrangeOrder(
        OrderStatus latestStatus = OrderStatus.Confirmed,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
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
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: OwnerUserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_admin_refund");
        order.AddOrderStatus(OrderStatusTrack.Create(latestStatus, order));

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    // A mobile (PaymentSheet) card order: T-0347 suppresses the Checkout Session, so the order's only
    // charge surface — and the only thing the refundable gate can key on — is the PaymentIntent.
    private Order ArrangeMobileOrder(
        OrderStatus latestStatus = OrderStatus.Confirmed,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
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
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: OwnerUserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripePaymentIntentId("pi_test_admin_refund");
        order.AddOrderStatus(OrderStatusTrack.Create(latestStatus, order));

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    private void ArrangeSeamSuccess(decimal amount, bool resolvedToExisting = false)
    {
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    RefundId: "refund-1",
                    RefundKey: $"refund:{req.OrderId}:admin:full",
                    Amount: amount,
                    Status: RefundStatus.Succeeded,
                    ResolvedToExisting: resolvedToExisting)));
    }

    private void ArrangeConsumedTotal(decimal total)
    {
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(total);
    }

    [Fact]
    public async Task Admin_RefundOnly_Confirmed_Paid_Leaves_Status_Confirmed_PaymentStatus_Refunded()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Lifecycle status UNCHANGED.
        Assert.Equal(OrderStatus.Confirmed, order.OrderStatusHistory
            .OrderByDescending(s => s.CreatedOn).First().Status);
        Assert.DoesNotContain(order.OrderStatusHistory, s => s.Status == OrderStatus.Cancelled);
        Assert.Equal(PaymentStatus.Refunded, result.Value!.PaymentStatus);
    }

    [Fact]
    public async Task Admin_RefundOnly_PartialConsumed_Returns_PartiallyRefunded()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 400m);
        ArrangeConsumedTotal(400m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.PartiallyRefunded, result.Value!.PaymentStatus);
    }

    [Fact]
    public async Task Admin_RefundOnly_GoesThroughSeam_WithFullRefundKey_AdminActor()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeConsumedTotal(1000m);
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:admin:full", 1000m, RefundStatus.Succeeded, false)));

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(OrderId, captured!.OrderId);
        Assert.Equal(AdminUserId, captured.ActorId);
        // A stable RefundRequestId means the full-refund key (refund:{OrderId}:admin:full) is one-per-order,
        // so a retry collapses on the same key — never an un-keyed inline Stripe call.
        Assert.False(string.IsNullOrEmpty(captured.RefundRequestId));
        Assert.Null(captured.DisputeId);
    }

    [Fact]
    public async Task Admin_RefundOnly_Retry_IsIdempotent_NoSecondStripeRefund()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeConsumedTotal(1000m);
        // The seam reports the second call collapsed on the existing succeeded refund: no second Stripe call.
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_RefundOnly_SeamFailure_ReturnsFailure_StatusUnchanged()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(new Error(
                "amount", BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundFailed, result.Error!.Message);
        Assert.Equal(OrderStatus.Confirmed, order.OrderStatusHistory
            .OrderByDescending(s => s.CreatedOn).First().Status);
    }

    [Fact]
    public async Task Admin_RefundOnly_NotPaidCard_Returns_OrderNotRefundable()
    {
        ArrangeOrder(OrderStatus.Confirmed, paymentStatus: PaymentStatus.Pending);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundOrderNotRefundable, result.Error!.Message);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // T-0348: a mobile-paid card order (StripeSessionId empty, PaymentIntentId set) clears the
    // refundable gate and goes through the seam — the gate now keys on the charge surface, not the
    // Session alone.
    [Fact]
    public async Task Admin_RefundOnly_MobilePaymentIntentOrder_PassesRefundableGate_GoesThroughSeam()
    {
        ArrangeMobileOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Refunded, result.Value!.PaymentStatus);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_RefundOnly_NotFound_Returns_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command("missing"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_RefundOnly_Success_EnqueuesOrderRefundedNotification()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pending.Verify(p => p.Enqueue(
            QueueNames.NotificationsDispatch,
            It.Is<QueueEnvelope<SendPushNotificationMessage>>(e =>
                e.Payload.EventKey == NotificationEventCatalog.OrderRefunded
                && e.Payload.UserId == OwnerUserId),
            MessageKeys.Push(OwnerUserId, NotificationEventCatalog.OrderRefunded, OrderId)),
            Times.Once);
    }
}
