using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Cleansia.Tests.Common;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// CancelOrder routed onto the one refund seam (ADR-0006): the handler no longer issues an inline,
/// un-keyed Stripe refund — it delegates the money call to <see cref="IRefundService"/> with
/// Reason=CustomerCancellation and the amount <c>order.Cancel(...)</c> computes. The confirm-then-record
/// payment-status flip lives in the seam (a Stripe failure reports RefundInitiated=false and the handler
/// never flips PaymentStatus itself), and the OrderRefunded notification is recorded through the shared
/// INotificationProducer seam.
/// </summary>
public class CancelOrderRefundSeamTests
{
    private const string OrderId = "order-cancel-1";
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<ICancellationPolicyResolver> _policyResolver = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    public CancelOrderRefundSeamTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _policyResolver
            .Setup(r => r.ResolveForOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(
                FreeCancellationHours: 48,
                PartialCancellationHours: 24,
                PartialCancellationFeeRate: 0.25m,
                LastMinuteCancellationFeeRate: 0.5m,
                OopsWindowMinutes: BookingPolicy.OopsWindowMinutesStandard,
                OopsWindowRule: OopsWindowRule.Standard));
    }

    private CancelOrder.Handler CreateHandler() =>
        new(
            OrderAccessDoubles.Over(_orderRepository, _session),
            _session.Object,
            new CustomerOrderCancellation(
                Mock.Of<ITenantProvider>(),
                _refundService.Object,
                _refundRepository.Object,
                Mock.Of<IReceivableRepository>(),
                _creditAccountRepository.Object,
                _loyaltyService.Object,
                _policyResolver.Object,
                _producer.Object,
                _liveActivityProducer.Object,
                _expressWaiverConsumer.Object,
                Mock.Of<IPendingDispatch>(),
                new AuditContext(),
                TimeProvider.System,
                NullLogger<CustomerOrderCancellation>.Instance));

    private Order ArrangeCardPaidPendingOrder()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(10),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
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
        _producer.Verify(p => p.NotifyAsync(
            UserId,
            NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            OrderId,
            It.IsAny<CancellationToken>()),
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
        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
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
        _producer.Verify(p => p.NotifyAsync(
            UserId,
            NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            OrderId,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// A Stripe outage on a member's cancel used to escape after the refund claim had committed the
    /// cancel: a 500, an order already Cancelled, and neither the card refund nor the credit share ever
    /// coming back. The cancel now completes, the refund stays pending for the hourly re-drive, and the
    /// credit share returns now on the refund's own key, so the re-drive cannot return it twice.
    /// </summary>
    [Theory]
    [MemberData(nameof(StripeTransportFaults))]
    public async Task A_Stripe_Outage_Leaves_The_Refund_Pending_And_Returns_The_Credit_Share_Now(Exception fault)
    {
        var order = ArrangeCardPaidPendingOrder();
        order.ApplyCredit(300m, UserId);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(fault);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.False(result.Value!.RefundInitiated);
        Assert.True(result.Value.RefundPending);
        VerifyCreditShareReturnedOnTheRefundKey(order, 300m);
        VerifyNoRefundNotice();
    }

    [Fact]
    public async Task A_Stripe_Refusal_Leaves_The_Refund_Pending_And_Returns_The_Credit_Share_Now()
    {
        var order = ArrangeCardPaidPendingOrder();
        order.ApplyCredit(300m, UserId);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RefundInitiated);
        Assert.True(result.Value.RefundPending);
        VerifyCreditShareReturnedOnTheRefundKey(order, 300m);
        VerifyNoRefundNotice();
    }

    /// <summary>
    /// A complaint on the order was already settled in 200 of credit, so 800 of the 1000 is left. The seam
    /// froze the card's 560 share of that 800; with the card refund left pending, the credit share of the
    /// same 800 comes back now, 240, so the re-drive's two legs and the settlement add up to the price.
    /// </summary>
    [Fact]
    public async Task A_Stripe_Outage_After_A_Complaint_Settled_In_Credit_Returns_The_Credit_Share_Net_Of_It()
    {
        var order = ArrangeCardPaidPendingOrder();
        order.ApplyCredit(300m, UserId);
        _creditAccountRepository
            .Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200m);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundPending);
        VerifyCreditShareReturnedOnTheRefundKey(order, 240m);
    }

    /// <summary>
    /// An admin already refunded 300 of the 1000 card order. A free cancellation asks the seam for the whole
    /// price, which sends the card the 700 not yet back, and reports that 700 as what this cancellation gives
    /// back, not the 1000 price.
    /// </summary>
    [Fact]
    public async Task A_Free_Cancel_Of_A_Partly_Refunded_Card_Order_Refunds_The_Rest_Of_The_Card()
    {
        var order = ArrangeCardPaidPendingOrder();
        order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(300m);
        ArrangeSeamSuccess(confirmedAmount: 700m);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        VerifyTheSeamWasAskedForThePrice();
        VerifyNoOrderEndedUnpaidReturn();
        Assert.True(result.Value!.RefundInitiated);
        Assert.Equal(700m, result.Value.RefundAmount);
        Assert.Equal(700m, result.Value.ActualRefundAmount);
        Assert.Equal(700m, order.CancellationRefundAmount);
    }

    /// <summary>
    /// A partly refunded order, 560 by card and 240 in credit of 800, with a complaint settled in 150 of credit.
    /// The seam is asked for the price and holds it to the 50 the sale has left, on both tenders; nothing comes
    /// back a second way, and the 50 is what the cancellation reports.
    /// </summary>
    [Fact]
    public async Task A_Partly_Refunded_Order_Asks_The_Seam_For_The_Price_And_Reports_What_The_Sale_Had_Left()
    {
        var order = ArrangeCardPaidPendingOrder();
        order.ApplyCredit(300m, UserId);
        order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(560m);
        _creditAccountRepository
            .Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(240m);
        _creditAccountRepository
            .Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(150m);
        ArrangeSeamSuccess(confirmedAmount: 35m);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        VerifyTheSeamWasAskedForThePrice();
        VerifyNoOrderEndedUnpaidReturn();
        Assert.Equal(50m, result.Value!.RefundAmount);
        Assert.Equal(35m, result.Value.ActualRefundAmount);
    }

    /// <summary>
    /// The same kind of partly refunded order, 280 by card and 120 in credit already back, cancelled while
    /// Stripe is down. The credit share of the rest comes back once, now, on the cancellation refund's own key,
    /// so the re-drive reads it as part of its slice; it does not come back a second time as the credit of an
    /// order that ended unrefunded.
    /// </summary>
    [Fact]
    public async Task A_Partly_Refunded_Order_Cancelled_While_Stripe_Is_Down_Returns_Its_Credit_Share_Once()
    {
        var order = ArrangeCardPaidPendingOrder();
        order.ApplyCredit(300m, UserId);
        order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(280m);
        _creditAccountRepository
            .Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(120m);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundPending);
        VerifyCreditShareReturnedOnTheRefundKey(order, 180m);
        VerifyNoOrderEndedUnpaidReturn();
    }

    /// <summary>
    /// The customer's credit account sits on the books of a company frozen for archive. The credit share that
    /// a cancellation returns at once while Stripe is down is raw SQL the frozen-books guard cannot see, so it
    /// is not written; the cancel still completes and the card refund still waits for the re-drive.
    /// </summary>
    [Fact]
    public async Task A_Stripe_Outage_Returns_No_Credit_Share_Onto_A_Frozen_Companys_Books()
    {
        var order = ArrangeCardPaidPendingOrder();
        order.ApplyCredit(300m, UserId);
        _creditAccountRepository
            .Setup(c => c.IsOnFrozenCompanyBooksAsync(UserId, order.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.True(result.Value!.RefundPending);
        _creditAccountRepository.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task A_Refund_That_Went_Through_Is_Not_Pending()
    {
        ArrangeCardPaidPendingOrder();
        ArrangeSeamSuccess(confirmedAmount: 1000m);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.Value!.RefundInitiated);
        Assert.False(result.Value.RefundPending);
    }

    public static TheoryData<Exception> StripeTransportFaults() =>
    [
        new HttpRequestException("connection reset"),
        new TimeoutException("gateway timeout"),
        new TaskCanceledException("the request timed out"),
    ];

    private void VerifyCreditShareReturnedOnTheRefundKey(Order order, decimal amount) =>
        _creditAccountRepository.Verify(c => c.TryReturnAsync(
            UserId,
            order.CurrencyId,
            amount,
            $"credit-return:refund:{OrderId}:cancel",
            UserId,
            It.IsAny<CancellationToken>(),
            OrderId,
            It.IsAny<string?>()), Times.Once);

    private void VerifyTheSeamWasAskedForThePrice() =>
        _refundService.Verify(s => s.IssueRefundAsync(
            It.Is<RefundRequest>(r => r.Amount == 1000m
                && r.Reason == RefundReason.CustomerCancellation
                && r.DisputeId == null
                && r.RefundRequestId == null),
            It.IsAny<CancellationToken>()), Times.Once);

    private void VerifyNoOrderEndedUnpaidReturn() =>
        _creditAccountRepository.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), $"credit-return:order-ended-unpaid:{OrderId}",
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);

    private void VerifyNoRefundNotice() =>
        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), NotificationEventCatalog.OrderRefunded, It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
}
