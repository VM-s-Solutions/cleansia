using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The platform-decided cancellation body, moved out of <c>AdminCancelOrder.Handler</c> so the
/// company wind-down can run it per order (ADR-0064 D2). What the handler's tests pinned still holds
/// here: the order is attributed to the caller's actor, the refund goes only through
/// <see cref="IRefundService"/> under the caller's reason (so the admin keeps <c>refund:{id}:cancel</c>
/// and the sweep's <c>ServiceNotRendered</c> resolves to <c>refund:{id}:admin</c>), a retry collapses
/// on the seam, credit comes home on an unpaid order, the waiver is released, every assigned cleaner
/// is told and loyalty is revoked. The refund leg stands alone for the re-drive.
/// </summary>
public class PlatformOrderCancellationTests
{
    private const string OrderId = "order-platform-cancel-1";
    private const string OwnerUserId = "owner-user";
    private const string ActorId = "actor-user";

    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    private PlatformOrderCancellation CreateService() =>
        new(
            _refundService.Object,
            _creditAccountRepository.Object,
            _loyaltyService.Object,
            _producer.Object,
            _liveActivityProducer.Object,
            _expressWaiverConsumer.Object);

    private static Order ArrangeOrder(
        OrderStatus latestStatus,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        bool paymentIntentOnly = false)
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
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: OwnerUserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        if (paymentIntentOnly)
        {
            order.AssignStripePaymentIntentId("pi_test_platform_cancel");
        }
        else
        {
            order.AssignStripeSessionId("cs_test_platform_cancel");
        }

        order.AddOrderStatus(OrderStatusTrack.Create(latestStatus, order));
        return order;
    }

    private void ArrangeSeamSuccess(decimal amount, bool resolvedToExisting = false)
    {
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    RefundId: "refund-1",
                    RefundKey: RefundService.BuildRefundKey(req),
                    Amount: amount,
                    Status: RefundStatus.Succeeded,
                    ResolvedToExisting: resolvedToExisting)));
    }

    private void ArrangeSeamFailure()
    {
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(new Error("Amount", BusinessErrorMessage.RefundFailed)));
    }

    [Fact]
    public async Task The_Order_Is_Cancelled_Fee_Free_Under_The_Callers_Attribution_And_Reason()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);

        var result = await CreateService().CancelAsync(
            order, ActorId, CancelledBy.System, OrderCancellationReasons.CompanyWindDown, RefundReason.ServiceNotRendered, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CancelledBy.System, order.CancelledBy);
        Assert.Equal(OrderCancellationReasons.CompanyWindDown, order.CancellationReason);
        Assert.Equal(0m, order.CancellationFeeRate);
        Assert.Equal(1000m, result.RefundAmount);
        Assert.True(result.Refund.Initiated);
    }

    [Fact]
    public async Task The_Refund_Goes_Through_The_Seam_Under_The_Callers_Reason_And_Actor()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:cancel", 1000m, RefundStatus.Succeeded, false)));

        await CreateService().CancelAsync(
            order, ActorId, CancelledBy.Admin, "incident", RefundReason.CustomerCancellation, CancellationToken.None);

        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(OrderId, captured!.OrderId);
        Assert.Equal(1000m, captured.Amount);
        Assert.Equal(RefundReason.CustomerCancellation, captured.Reason);
        Assert.Equal(ActorId, captured.ActorId);
        Assert.Null(captured.DisputeId);
        Assert.Equal($"refund:{OrderId}:cancel", RefundService.BuildRefundKey(captured));
    }

    [Fact]
    public async Task A_ServiceNotRendered_Refund_Resolves_To_The_Admin_Key()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:admin", 1000m, RefundStatus.Succeeded, false)));

        await CreateService().CancelAsync(
            order, ActorId, CancelledBy.System, OrderCancellationReasons.CompanyWindDown, RefundReason.ServiceNotRendered, CancellationToken.None);

        Assert.Equal($"refund:{OrderId}:admin", RefundService.BuildRefundKey(captured!));
    }

    [Fact]
    public async Task A_PaymentIntent_Only_Order_Still_Refunds_Through_The_Seam()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed, paymentIntentOnly: true);
        ArrangeSeamSuccess(amount: 1000m);

        var result = await CreateService().CancelAsync(
            order, ActorId, CancelledBy.Admin, null, RefundReason.CustomerCancellation, CancellationToken.None);

        Assert.True(result.Refund.Initiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Retry_Resolved_To_The_Existing_Refund_Still_Counts_As_Initiated()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m, resolvedToExisting: true);

        var result = await CreateService().CancelAsync(
            order, ActorId, CancelledBy.Admin, null, RefundReason.CustomerCancellation, CancellationToken.None);

        Assert.True(result.Refund.Initiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Successful_Refund_Notifies_The_Customer_Keyed_On_The_Refund()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);

        await CreateService().CancelAsync(
            order, ActorId, CancelledBy.Admin, null, RefundReason.CustomerCancellation, CancellationToken.None);

        _producer.Verify(p => p.NotifyAsync(
            OwnerUserId,
            NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            It.Is<string>(subject => !string.IsNullOrWhiteSpace(subject) && subject != OrderId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_Refused_Refund_Leaves_The_Order_Cancelled_Reports_The_Failure_And_Tells_Nobody_Of_Money()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamFailure();

        var result = await CreateService().CancelAsync(
            order, ActorId, CancelledBy.System, OrderCancellationReasons.CompanyWindDown, RefundReason.ServiceNotRendered, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.True(result.Refund.Attempted);
        Assert.False(result.Refund.Initiated);
        Assert.Equal(BusinessErrorMessage.RefundFailed, result.Refund.FailureMessage);
        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), NotificationEventCatalog.OrderRefunded, It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task An_Unpaid_Order_Refunds_Nothing_And_Returns_All_Of_Its_Credit()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed, paymentStatus: PaymentStatus.Pending);
        order.ApplyCredit(300m, OwnerUserId);

        var result = await CreateService().CancelAsync(
            order, ActorId, CancelledBy.Admin, null, RefundReason.CustomerCancellation, CancellationToken.None);

        Assert.False(result.Refund.Attempted);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _creditAccountRepository.Verify(
            r => r.TryReturnAsync(
                OwnerUserId, order.CurrencyId, 300m, $"credit-return:order-ended-unpaid:{OrderId}", ActorId,
                It.IsAny<CancellationToken>(), OrderId, null),
            Times.Once);
    }

    [Fact]
    public async Task A_Paid_Cash_Order_Is_Cancelled_Without_A_Refund_Or_A_Credit_Return()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Paid);

        var result = await CreateService().CancelAsync(
            order, ActorId, CancelledBy.System, OrderCancellationReasons.CompanyWindDown, RefundReason.ServiceNotRendered, CancellationToken.None);

        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.False(result.Refund.Attempted);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _creditAccountRepository.Verify(
            r => r.TryReturnAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task The_Waiver_Is_Released_Loyalty_Revoked_And_The_Activity_Ended()
    {
        var order = ArrangeOrder(OrderStatus.Confirmed);
        ArrangeSeamSuccess(amount: 1000m);

        await CreateService().CancelAsync(
            order, ActorId, CancelledBy.Admin, null, RefundReason.CustomerCancellation, CancellationToken.None);

        _expressWaiverConsumer.Verify(c => c.ReleaseForOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _loyaltyService.Verify(l => l.RevokeForCancelledOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _liveActivityProducer.Verify(l => l.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, It.IsAny<OrderStatusTrack>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_Refund_Leg_Alone_Re_Drives_The_Same_Key_And_Notifies_On_Success()
    {
        var order = ArrangeOrder(OrderStatus.Cancelled);
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:admin", 1000m, RefundStatus.Succeeded, false)));

        var outcome = await CreateService().RefundAsync(order, ActorId, RefundReason.ServiceNotRendered, CancellationToken.None);

        Assert.True(outcome.Initiated);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal($"refund:{OrderId}:admin", RefundService.BuildRefundKey(captured!));
        Assert.Equal(ActorId, captured!.ActorId);
        _producer.Verify(p => p.NotifyAsync(
            OwnerUserId, NotificationEventCatalog.OrderRefunded, It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), "refund-1", It.IsAny<CancellationToken>()), Times.Once);
        _loyaltyService.Verify(l => l.RevokeForCancelledOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
