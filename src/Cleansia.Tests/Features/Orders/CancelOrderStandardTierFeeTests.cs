using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The standard-tier (non-member) money path through the customer <see cref="CancelOrder.Handler"/>,
/// driven through the REAL <see cref="CancellationPolicyResolver"/> with no active membership — the
/// exact production wiring a non-member hits. The wiring/seam suites only ever schedule cleaning far
/// in the future (free tier), so they are blind to whether the partial and last-minute fees still fire
/// once the resolver supplies its absolute 24h window into the policy. These pin that an accepted
/// standard cancellation 12h / 1h before start is charged 0.25 / 0.50 and refunds only the remainder —
/// the guard against an override-semantics slip that would collapse the free window to 0 and refund
/// every standard cancellation in full.
/// </summary>
public class CancelOrderStandardTierFeeTests
{
    private const string OrderId = "order-std-1";
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public CancelOrderStandardTierFeeTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        // No active membership → the resolver returns the standard absolute 24h window, the value the
        // handler passes as freeCancellationHoursOverride for every non-member.
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
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
            new CancellationPolicyResolver(_membershipRepository.Object),
            _pending.Object);

    private Order ArrangeAcceptedCardPaidOrder(DateTime cleaningUtc, decimal totalPrice)
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
            cleaningDateTime: cleaningUtc,
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId);
        order.Id = OrderId;
        // Created well before the oops window so the short-circuit cannot mask the tier.
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_std");

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("tester", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    [Fact]
    public async Task StandardTier_Accepted_12hBeforeStart_Charges25Percent_RefundsRemainder()
    {
        // 12h before start → between 4h and 24h → partial tier 0.25. Hand-derived refund: 1000 × (1 − 0.25) = 750.
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(12), totalPrice: 1000m);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, result.Value!.FeeRate);
        Assert.Equal(750m, result.Value.RefundAmount);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, order.CancellationFeeRate);
        Assert.Equal(750m, order.CancellationRefundAmount);
    }

    [Fact]
    public async Task StandardTier_Accepted_1hBeforeStart_Charges50Percent_RefundsRemainder()
    {
        // 1h before start → below 4h → last-minute tier 0.50. Hand-derived refund: 1000 × (1 − 0.50) = 500.
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(1), totalPrice: 1000m);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, result.Value!.FeeRate);
        Assert.Equal(500m, result.Value.RefundAmount);
        Assert.Equal(BookingPolicy.LastMinuteCancellationFeeRate, order.CancellationFeeRate);
        Assert.Equal(500m, order.CancellationRefundAmount);
    }

    [Fact]
    public async Task Refund_RoundsToTwoDecimals_AwayFromZero_AtTheTruncationBoundary()
    {
        // 100.01 × (1 − 0.50) = 50.005 — a 3rd-decimal .5 boundary. Unrounded, the
        // Refund row (numeric(18,2)) and Stripe ((long)(amount*100) truncation)
        // would disagree by a cent (50.01 vs 50.00). The source-rounding fix
        // (T-0355) pins one value, away-from-zero, that every reader shares.
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(1), totalPrice: 100.01m);

        RefundRequest? issued = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
            {
                issued = req;
                return BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false));
            });

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50.01m, result.Value!.RefundAmount);
        Assert.Equal(50.01m, order.CancellationRefundAmount);
        Assert.Equal(50.01m, issued!.Amount);
    }
}
