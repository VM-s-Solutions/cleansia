using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// AC5/AC6 — the customer <see cref="CancelOrder.Handler"/> formula-to-response wiring and the
/// illegal-state rejections, the gaps the seam suite (<c>CancelOrderRefundSeamTests</c>) leaves open.
/// The seam suite proves the money call is delegated correctly; these prove (a) the
/// <c>Response.FeeRate/RefundAmount/TotalPrice</c> equal the policy formula and <c>Order.Cancel</c> is
/// persisted with the SAME numbers, (b) the refund branch fires ONLY when
/// <c>Card &amp;&amp; Paid &amp;&amp; refundAmount &gt; 0 &amp;&amp; a refundable charge surface is
/// present</c> — i.e. a Checkout Session (web) OR a PaymentIntent (mobile, T-0347/T-0348), via
/// <c>Order.HasRefundableChargeSurface</c> — and skips only when NEITHER surface exists, and (c) a
/// terminal latest status returns the matching error and computes no refund.
///
/// The policy resolver returns the DEFAULT window (24h/4h/0.25/0.50) so the expected fee rate is the
/// hand-derived tier, not a Plus-widened one.
/// </summary>
public class CancelOrderRefundWiringTests
{
    private const string OrderId = "order-1";
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<ICancellationPolicyResolver> _policyResolver = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();

    public CancelOrderRefundWiringTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _policyResolver
            .Setup(r => r.ResolveForUserAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CancellationPolicy(
                FreeCancellationHours: BookingPolicy.FreeCancellationHours,
                PartialCancellationHours: BookingPolicy.PartialCancellationHours,
                PartialCancellationFeeRate: BookingPolicy.PartialCancellationFeeRate,
                LastMinuteCancellationFeeRate: BookingPolicy.LastMinuteCancellationFeeRate));
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false)));
    }

    private CancelOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _refundService.Object,
            _loyaltyService.Object,
            _policyResolver.Object,
            _producer.Object,
            _liveActivityProducer.Object);

    private void Arrange(Order order) =>
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());

    /// <summary>
    /// A confirmed (accepted) card-paid order whose cleaning is far in the future, so the FREE tier
    /// applies (feeRate 0) and the entire price is refundable.
    /// </summary>
    private static Order AcceptedFreeTierOrder(decimal totalPrice = 1000m) =>
        OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            totalPrice: totalPrice,
            orderId: OrderId,
            userId: UserId);

    // ── AC5: formula → response and Order.Cancel wiring ──

    [Fact]
    public async Task FreeTier_Response_And_OrderCancel_CarryFullRefund_ZeroFeeRate()
    {
        var order = AcceptedFreeTierOrder(totalPrice: 1000m);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Cleaning is +10 days, accepted → FREE tier → fee 0, full refund (hand-derived: 1000 × (1 − 0)).
        Assert.Equal(0m, result.Value!.FeeRate);
        Assert.Equal(1000m, result.Value.RefundAmount);
        Assert.Equal(1000m, result.Value.TotalPrice);
        // Order.Cancel persisted the SAME numbers the response reports.
        Assert.Equal(0m, order.CancellationFeeRate);
        Assert.Equal(1000m, order.CancellationRefundAmount);
        Assert.Equal(CancelledBy.Customer, order.CancelledBy);
    }

    [Fact]
    public async Task Response_RefundAmount_EqualsTotalPriceTimesOneMinusFeeRate_ForArbitraryTotal()
    {
        var order = AcceptedFreeTierOrder(totalPrice: 1234.56m);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Hand-derived: feeRate 0 (free tier) → 1234.56 × 1 = 1234.56, exact decimal.
        Assert.Equal(1234.56m, result.Value!.RefundAmount);
        Assert.Equal(result.Value.TotalPrice * (1m - result.Value.FeeRate), order.CancellationRefundAmount);
    }

    [Fact]
    public async Task RefundBranch_Fires_OnceThroughSeam_ForCardPaidWithSessionAndPositiveRefund()
    {
        var order = AcceptedFreeTierOrder();
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // T-0348: a mobile (PaymentSheet) card order has NO Checkout Session — its only charge surface is the
    // PaymentIntent (T-0347 suppresses the Session). Cancelling such a paid order MUST still refund (under
    // master the session-only gate silently kept the money). The refund branch fires on the intent surface.
    [Fact]
    public async Task RefundBranch_Fires_OnceThroughSeam_ForCardPaidWithPaymentIntentOnly()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            orderId: OrderId, userId: UserId,
            stripeSessionId: null, stripePaymentIntentId: "pi_test_cancel");
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC5: the refund-branch guard — each disqualifying condition must skip the seam ──

    [Fact]
    public async Task RefundBranch_Skipped_WhenPaymentTypeIsNotCard()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            orderId: OrderId, userId: UserId, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Paid);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundBranch_Skipped_WhenPaymentStatusIsNotPaid()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            orderId: OrderId, userId: UserId, paymentType: PaymentType.Card, paymentStatus: PaymentStatus.Pending);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The refund branch keys on Order.HasRefundableChargeSurface, so it skips ONLY when neither a
    // Checkout Session nor a PaymentIntent is present (both explicitly null here). A session-only OR an
    // intent-only order DOES refund — those positive paths are pinned above.
    [Fact]
    public async Task RefundBranch_Skipped_WhenNoChargeSurface_NeitherSessionNorIntent()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            orderId: OrderId, userId: UserId,
            stripeSessionId: null, stripePaymentIntentId: null);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundBranch_Skipped_WhenRefundAmountIsZero_FullFeeLastMinute()
    {
        // Accepted, cleaning in 30 minutes → LAST-MINUTE tier 0.50. But to make refundAmount EXACTLY 0
        // we need feeRate 1; the tiers never reach 1, so instead assert the positive-refund guard via a
        // zero-priced order: 0 × (1 − feeRate) = 0 → branch must skip even though Card/Paid/surface hold.
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            totalPrice: 0m, orderId: OrderId, userId: UserId);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value!.RefundAmount);
        Assert.False(result.Value.RefundInitiated);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── AC6: illegal-state rejections — error code + no refund computed ──

    [Fact]
    public async Task LatestStatusCancelled_Returns_OrderAlreadyCancelled_NoRefund()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.Cancelled],
            orderId: OrderId, userId: UserId);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCancelled, result.Error!.Message);
        Assert.Null(order.CancellationRefundAmount);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LatestStatusCompleted_Returns_OrderAlreadyCompleted_NoRefund()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed],
            orderId: OrderId, userId: UserId);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderAlreadyCompleted, result.Error!.Message);
        Assert.Null(order.CancellationRefundAmount);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LatestStatusInProgress_Returns_OrderInProgressCannotCancel_NoRefund()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress],
            orderId: OrderId, userId: UserId);
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, result.Error!.Message);
        Assert.Null(order.CancellationRefundAmount);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CrossUser_Returns_OrderNotFound_NoRefund()
    {
        var order = OrderMockFactory.GenerateWithStatusHistory(
            [OrderStatus.New, OrderStatus.Confirmed],
            orderId: OrderId, userId: "a-different-owner");
        Arrange(order);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Null(order.CancellationRefundAmount);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
