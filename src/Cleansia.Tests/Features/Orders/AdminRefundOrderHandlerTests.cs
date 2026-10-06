using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Queue.Abstractions;
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
    private readonly Mock<ILoyaltyService> _loyalty = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<IOutboxMessageRepository> _outbox = new();

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
            _loyalty.Object,
            _session.Object,
            _producer.Object,
            _outbox.Object,
            _auditContext);

    private Order ArrangeOrder(
        OrderStatus latestStatus = OrderStatus.Confirmed,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string? userId = OwnerUserId)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
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
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: OwnerUserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
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

    private void ArrangeSeamSuccess(decimal amount, bool resolvedToExisting = false, Action? settles = null)
    {
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => settles?.Invoke())
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
        ArrangeSeamSuccess(amount: 1000m, settles: () => order.UpdatePaymentStatus(PaymentStatus.Refunded));
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Lifecycle status UNCHANGED.
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.DoesNotContain(order.OrderStatusHistory, s => s.Status == OrderStatus.Cancelled);
        Assert.Equal(PaymentStatus.Refunded, result.Value!.PaymentStatus);
    }

    [Fact]
    public async Task Admin_RefundOnly_PartialConsumed_Returns_PartiallyRefunded()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 400m, settles: () => order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded));
        ArrangeConsumedTotal(400m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.PartiallyRefunded, result.Value!.PaymentStatus);
    }

    /// <summary>
    /// A complaint was settled in 300 of credit, so the full refund sent the card 700 and the seam stored the
    /// order as Refunded: the sale has nothing left to give back. The admin is told what was stored, and so is
    /// the audit row.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_After_A_Settlement_Reports_The_Status_The_Seam_Stored()
    {
        var order = ArrangeOrder(OrderStatus.Completed);
        ArrangeSeamSuccess(amount: 700m, settles: () => order.UpdatePaymentStatus(PaymentStatus.Refunded));
        ArrangeConsumedTotal(700m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PaymentStatus.Refunded, result.Value!.PaymentStatus);
        Assert.Equal(order.PaymentStatus, result.Value.PaymentStatus);
        Assert.Contains("\"paymentStatus\":\"refunded\"", _auditContext.DrainSnapshot()!.AfterJson, StringComparison.Ordinal);
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
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
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
        var order = ArrangeMobileOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m, settles: () => order.UpdatePaymentStatus(PaymentStatus.Refunded));
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
    public async Task Admin_RefundOnly_Success_RecordsOrderRefundedNotification()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _producer.Verify(p => p.NotifyAsync(
            OwnerUserId,
            NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
                It.Is<string>(subject => !string.IsNullOrWhiteSpace(subject)
                    && subject != OrderId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A full refund takes back every point the order still holds (customer terms §11): it hands the
    /// clawback the whole 1000 price — not the 600 the card leg happened to return — so the share it asks
    /// for is the whole earn and the cap trims it to what earlier refunds left. Keyed on the refund, so a
    /// replay takes nothing twice.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_TakesBackEveryPointLeft_KeyedOnTheRefund()
    {
        var order = ArrangeOrder(OrderStatus.Completed);
        ArrangeSeamSuccess(amount: 600m);
        ArrangeConsumedTotal(600m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _loyalty.Verify(l => l.RevokeForRefundAsync(
            OrderId, order.TotalPrice, $"refund:{OrderId}:admin:full", AdminUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        _loyalty.VerifyNoOtherCalls();
    }

    /// <summary>
    /// The full refund settled and flipped the order to Refunded, then its clawback failed. The refund's
    /// notice was staged in the same unit of work as the clawback and was lost with it. Running the command
    /// again is the way back to both: the seam answers with the settled refund and moves no money, the
    /// clawback is handed the whole 1000 under the same key, and the notice the customer never got is queued.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_Rerun_After_Its_Refund_Settled_Runs_The_Clawback_And_Sends_The_Lost_Notice()
    {
        var order = ArrangeOrder(OrderStatus.Completed, paymentStatus: PaymentStatus.Refunded);
        ArrangeOwnFullRefund(RefundStatus.Succeeded);
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(PaymentStatus.Refunded, result.Value!.PaymentStatus);
        _loyalty.Verify(l => l.RevokeForRefundAsync(
            OrderId, order.TotalPrice, $"refund:{OrderId}:admin:full", AdminUserId, It.IsAny<CancellationToken>()),
            Times.Once);
        _producer.Verify(p => p.NotifyAsync(
                OwnerUserId,
                NotificationEventCatalog.OrderRefunded,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                "refund-1",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// The first call's notice committed, so the re-run queues none: a second row on its key would fail the
    /// commit on the outbox's unique index. Its clawback takes the points the failed one left, so it succeeds.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_Rerun_Whose_Notice_Is_Already_Queued_Sends_No_Second_One()
    {
        ArrangeOrder(OrderStatus.Completed, paymentStatus: PaymentStatus.Refunded);
        ArrangeOwnFullRefund(RefundStatus.Succeeded);
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);
        ArrangeConsumedTotal(1000m);
        ArrangeNoticeAlreadyQueued();
        ArrangeClawbackTakes(true);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _producer.Verify(p => p.NotifyAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// A re-run after everything landed: the seam answers with the settled refund and moves no money, the
    /// notice is already queued and the clawback finds nothing left to take. It used to answer that a refund
    /// was issued; it is refused, saying there is nothing left to refund.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_Rerun_With_Nothing_Left_To_Do_Is_Refused()
    {
        ArrangeOrder(OrderStatus.Completed, paymentStatus: PaymentStatus.Refunded);
        ArrangeOwnFullRefund(RefundStatus.Succeeded);
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);
        ArrangeConsumedTotal(1000m);
        ArrangeNoticeAlreadyQueued();
        ArrangeClawbackTakes(false);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        _loyalty.Verify(l => l.RevokeForRefundAsync(
            OrderId, 1000m, $"refund:{OrderId}:admin:full", AdminUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A guest's order has no notice to send and no points to take, so its re-run has nothing to do at all.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_Rerun_On_A_Guest_Order_Is_Refused()
    {
        ArrangeOrder(OrderStatus.Completed, paymentStatus: PaymentStatus.Refunded, userId: null);
        ArrangeOwnFullRefund(RefundStatus.Succeeded);
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);
        ArrangeConsumedTotal(1000m);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
    }

    /// <summary>
    /// The first refund always answers that it was issued, whatever the clawback finds: an order that never
    /// completed has no points to take, and the money still went back.
    /// </summary>
    [Fact]
    public async Task Admin_FullRefund_First_Run_Succeeds_When_The_Clawback_Has_Nothing_To_Take()
    {
        ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);
        ArrangeConsumedTotal(1000m);
        ArrangeNoticeAlreadyQueued();
        ArrangeClawbackTakes(false);

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.RefundInitiated);
    }

    private void ArrangeNoticeAlreadyQueued()
    {
        var pushKey = MessageKeys.Push(OwnerUserId, NotificationEventCatalog.OrderRefunded, "refund-1");
        _outbox
            .Setup(o => o.GetByQueueAndKeyAsync(QueueNames.NotificationsDispatch, pushKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OutboxMessage.Create(QueueNames.NotificationsDispatch, pushKey, "{}", "tenant-1"));
    }

    private void ArrangeClawbackTakes(bool tookPoints) =>
        _loyalty
            .Setup(l => l.RevokeForRefundAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tookPoints);

    /// <summary>
    /// Only the order's own settled full refund reopens the command. An order refunded some other way —
    /// partial refunds, a cancellation, a dispute — or whose full refund never settled stays refused, and
    /// the seam is never asked.
    /// </summary>
    [Theory]
    [InlineData(PaymentStatus.Refunded, null)]
    [InlineData(PaymentStatus.PartiallyRefunded, null)]
    [InlineData(PaymentStatus.PartiallyRefunded, RefundStatus.Pending)]
    [InlineData(PaymentStatus.Refunded, RefundStatus.Failed)]
    public async Task Admin_FullRefund_On_An_Order_Not_Paid_Without_Its_Own_Settled_Refund_Stays_Refused(
        PaymentStatus paymentStatus, RefundStatus? ownFullRefund)
    {
        ArrangeOrder(OrderStatus.Completed, paymentStatus);
        if (ownFullRefund is { } status)
        {
            ArrangeOwnFullRefund(status);
        }

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundOrderNotRefundable, result.Error!.Message);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _loyalty.VerifyNoOtherCalls();
    }

    private void ArrangeOwnFullRefund(RefundStatus status)
    {
        var refund = Refund.Create(
            OrderId, $"refund:{OrderId}:admin:full", 1000m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        if (status == RefundStatus.Succeeded)
        {
            refund.MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow);
        }
        else if (status == RefundStatus.Failed)
        {
            refund.MarkFailed();
        }

        _refundRepository
            .Setup(r => r.GetByRefundKeyAsync($"refund:{OrderId}:admin:full", It.IsAny<CancellationToken>()))
            .ReturnsAsync(refund);
    }

    [Fact]
    public async Task Admin_FullRefund_SeamFailure_TakesNoPoints()
    {
        ArrangeOrder(OrderStatus.Completed);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(new Error(
                "amount", BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(
            new AdminRefundOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        _loyalty.VerifyNoOtherCalls();
    }
}
