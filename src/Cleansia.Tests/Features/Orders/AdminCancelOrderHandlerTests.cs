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
/// AC2 / AC3 — the admin-cancel command. An admin (whose <c>sub</c> is NOT the order's UserId) can
/// cancel ANY order: there is no ownership gate (the rejection that blocks admins on the customer
/// CancelOrder path is absent here), the order is attributed to <see cref="CancelledBy.Admin"/>, and the
/// terminal-state guards (Cancelled / Completed / InProgress) still surface the existing
/// <see cref="BusinessErrorMessage"/> codes. The Stripe refund is issued ONLY through
/// <see cref="IRefundService"/> with the deterministic cancel key, so a retried admin cancel cannot
/// double-refund (the seam reports ResolvedToExisting and no second Stripe call is made).
/// </summary>
public class AdminCancelOrderHandlerTests
{
    private const string OrderId = "order-admin-cancel-1";
    private const string OwnerUserId = "owner-user";
    private const string AdminUserId = "admin-user";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public AdminCancelOrderHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminUserId);
    }

    private AdminCancelOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _refundService.Object,
            _loyaltyService.Object,
            _pending.Object);

    private Order ArrangeOrder(OrderStatus latestStatus, PaymentStatus paymentStatus = PaymentStatus.Paid)
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
            // OWNED BY A DIFFERENT USER — proves the admin path has no ownership gate.
            userId: OwnerUserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_admin_cancel");
        order.AddOrderStatus(OrderStatusTrack.Create(latestStatus, order));

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void ArrangeSeamSuccess(decimal amount, bool resolvedToExisting = false)
    {
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    RefundId: "refund-1",
                    RefundKey: $"refund:{req.OrderId}:cancel",
                    Amount: amount,
                    Status: RefundStatus.Succeeded,
                    ResolvedToExisting: resolvedToExisting)));
    }

    [Fact]
    public async Task Admin_Cancels_NonOwned_Confirmed_Order_Succeeds_AttributedToAdmin()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, "incident"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CancelledBy.Admin, order.CancelledBy);
        Assert.Equal(OrderStatus.Cancelled, order.OrderStatusHistory
            .OrderByDescending(s => s.CreatedOn).First().Status);
    }

    [Fact]
    public async Task Admin_Cancel_Completed_Order_Returns_OrderAlreadyCompleted()
    {
        ArrangeOrder(OrderStatus.Completed);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCompleted, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Cancel_AlreadyCancelled_Order_Returns_OrderAlreadyCancelled()
    {
        ArrangeOrder(OrderStatus.Cancelled);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCancelled, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Cancel_InProgress_Order_Returns_OrderInProgressCannotCancel()
    {
        ArrangeOrder(OrderStatus.InProgress);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, result.Error!.Message);
    }

    [Fact]
    public async Task Admin_Cancel_WithRefund_GoesThroughSeam_WithCancelKey_AndCustomerCancellationReason()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:cancel", 1000m, RefundStatus.Succeeded, false)));

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(OrderId, captured!.OrderId);
        // The cancel purpose key (refund:{OrderId}:cancel) is one-per-order, so admin + customer cancels
        // and any retry collapse onto the same refund — never an un-keyed inline Stripe call.
        Assert.Equal(RefundReason.CustomerCancellation, captured.Reason);
        Assert.Equal(AdminUserId, captured.ActorId);
        Assert.Null(captured.DisputeId);
    }

    [Fact]
    public async Task Admin_Cancel_Retry_DoesNotDoubleRefund_ResolveToExisting_StillSucceeds()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        // The seam reports the second call collapsed onto the existing succeeded refund (same cancel key):
        // no second Stripe refund was issued. The handler still reports success + RefundInitiated.
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_Cancel_RefundSuccess_EnqueuesOrderRefundedNotification()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pending.Verify(p => p.Enqueue(
            QueueNames.NotificationsDispatch,
            It.Is<QueueEnvelope<SendPushNotificationMessage>>(e =>
                e.Payload.EventKey == NotificationEventCatalog.OrderRefunded
                && e.Payload.UserId == OwnerUserId),
            MessageKeys.Push(OwnerUserId, NotificationEventCatalog.OrderRefunded, OrderId)),
            Times.Once);
    }

    [Fact]
    public async Task Admin_Cancel_NotFound_Returns_OrderNotFound()
    {
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<Order>().AsQueryable().BuildMock());

        var result = await CreateHandler().Handle(
            new AdminCancelOrder.Command("missing", null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
    }
}
