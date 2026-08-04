using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// T-0526 AC2 — the preview and the cancellation are the SAME arithmetic.
///
/// <para>Every case drives BOTH production handlers over ONE order fixture, in the order a customer
/// meets them: the preview is quoted, then the cancellation is executed. The assertion is that the
/// quoted <c>FeeRate</c>/<c>RefundAmount</c> equal the charged ones to the cent — and, separately,
/// that each equals a hand-derived number, so a build where both sides return zero cannot pass by
/// agreeing with itself.</para>
///
/// <para>Nothing here re-implements the schedule: the fixture varies only the inputs the schedule
/// reads (lead time, booking age, whether a cleaner is on the job, the member's own free window) and
/// lets both handlers answer. That is the point — if the two could ever disagree, a customer is told
/// one number and charged another.</para>
/// </summary>
public class CancellationFeePreviewAgreementTests
{
    private const string OrderId = "order-preview-1";
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    public CancellationFeePreviewAgreementTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false)));
    }

    // Both handlers share ONE resolver instance backed by ONE membership repository, which is what the
    // DI container gives them per request — a member's window reaches the quote and the charge alike.
    private CancellationPolicyResolver Resolver => new(_membershipRepository.Object);

    private CancelOrder.Handler CreateCancelHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            _refundService.Object,
            _loyaltyService.Object,
            Resolver,
            _producer.Object,
            _liveActivityProducer.Object,
            _expressWaiverConsumer.Object);

    private GetCancellationFeePreview.Handler CreatePreviewHandler() =>
        new(
            _orderRepository.Object,
            _session.Object,
            Resolver,
            _expressWaiverConsumer.Object);

    private Order ArrangeOrder(
        double cleaningInHours,
        bool assignCleaner = true,
        decimal totalPrice = 1000m,
        int bookedMinutesAgo = 120)
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
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId);
        order.Id = OrderId;
        order.Created("tester", DateTime.UtcNow.AddMinutes(-bookedMinutesAgo));
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_preview");

        var track = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
        track.Created("tester", DateTimeOffset.UtcNow.AddMinutes(-bookedMinutesAgo));
        order.AddOrderStatus(track);

        if (assignCleaner)
        {
            var cleanerUser = User.CreateWithPassword("cleaner@cleansia.test", "Passw0rd!", "Clean", "Er");
            cleanerUser.Id = "emp-preview-user";
            var cleaner = Employee.CreateWithUser(cleanerUser);
            cleaner.Id = "emp-preview";
            order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        }

        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void GivenPlusMembership(int freeCancellationWindowHours)
    {
        var plan = MembershipPlan.Create(
            code: "PLUS",
            name: "Cleansia Plus",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_plus",
            discountPercentage: 10m,
            freeCancellationWindowHours: freeCancellationWindowHours,
            allowsExpressUpgrade: true);
        var membership = UserMembership.Create(
            userId: UserId,
            membershipPlanId: plan.Id,
            stripeSubscriptionId: "sub_1",
            currentPeriodStart: DateTime.UtcNow.AddDays(-1),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        typeof(UserMembership).GetProperty(nameof(UserMembership.MembershipPlan))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(membership, [plan]);

        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    private async Task<GetCancellationFeePreview.Response> PreviewAsync()
    {
        var result = await CreatePreviewHandler().Handle(
            new GetCancellationFeePreview.Query(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private async Task<CancelOrder.Response> CancelAsync()
    {
        var result = await CreateCancelHandler().Handle(
            new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    /// <summary>Quote, then commit — and assert the two agree AND land on the hand-derived numbers.</summary>
    private async Task AssertQuoteMatchesChargeAsync(
        CancellationFeeTier expectedTier, decimal expectedRate, decimal expectedRefund, decimal expectedFee)
    {
        var preview = await PreviewAsync();
        var cancel = await CancelAsync();

        Assert.Equal(expectedTier, preview.Tier);
        Assert.Equal(expectedRate, preview.FeeRate);
        Assert.Equal(expectedRefund, preview.RefundAmount);
        Assert.Equal(expectedFee, preview.FeeAmount);

        Assert.Equal(cancel.FeeRate, preview.FeeRate);
        Assert.Equal(cancel.RefundAmount, preview.RefundAmount);
        Assert.Equal(cancel.TotalPrice, preview.TotalPrice);
    }

    // ── The tier ladder ──

    [Fact]
    public async Task Accepted_TwoDaysOut_Quotes_Free_And_Charges_Free()
    {
        ArrangeOrder(cleaningInHours: 48);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.FreeOutsideWindow, 0m, 1000m, 0m);
    }

    [Fact]
    public async Task Accepted_TwelveHoursOut_Quotes_25Percent_And_Charges_25Percent()
    {
        // 12h before start → [4, 24) → 0.25. Hand-derived: refund 1000 × 0.75 = 750, fee 250.
        ArrangeOrder(cleaningInHours: 12);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.Partial, 0.25m, 750m, 250m);
    }

    [Fact]
    public async Task Accepted_OneHourOut_Quotes_50Percent_And_Charges_50Percent()
    {
        // 1h before start → < 4h → 0.50. Hand-derived: refund 1000 × 0.50 = 500, fee 500.
        ArrangeOrder(cleaningInHours: 1);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.LastMinute, 0.50m, 500m, 500m);
    }

    // ── The oops window ──

    [Fact]
    public async Task Accepted_Inside_The_Oops_Window_Quotes_Free_And_Charges_Free()
    {
        // Booked 5 minutes ago with the cleaning an hour away: the 50% tier by timing, free by the
        // accidental-tap rule. Strictly inside the 15-minute cap — both handlers read DateTime.UtcNow,
        // so the exact boundary is pinned deterministically by the pure-function suite instead.
        ArrangeOrder(cleaningInHours: 1, bookedMinutesAgo: 5);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.FreeOopsWindow, 0m, 1000m, 0m);
    }

    // ── No cleaner on the job ──

    [Fact]
    public async Task No_Cleaner_Assigned_Quotes_Free_And_Charges_Free_Even_LastMinute()
    {
        // The T-0525 predicate, on the preview: an hour before start with nobody dispatched is free on
        // both surfaces. A preview keyed on OrderStatus.Confirmed would quote 50% here — the fixture
        // carries a Confirmed track and no assignment row precisely to catch that.
        ArrangeOrder(cleaningInHours: 1, assignCleaner: false);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.FreeNotAccepted, 0m, 1000m, 0m);
    }

    // ── The member's own window ──

    [Fact]
    public async Task Plus_Member_Six_Hours_Out_Quotes_Free_And_Charges_Free()
    {
        GivenPlusMembership(freeCancellationWindowHours: 4);
        ArrangeOrder(cleaningInHours: 6);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.FreeOutsideWindow, 0m, 1000m, 0m);
    }

    [Fact]
    public async Task NonMember_Six_Hours_Out_Quotes_25Percent_And_Charges_25Percent()
    {
        // The same order, same instant, no membership: the standard 24h window makes it the 25% tier.
        // No client can tell these two cases apart, which is why the server has to answer.
        ArrangeOrder(cleaningInHours: 6);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.Partial, 0.25m, 750m, 250m);
    }

    // ── Rounding ──

    [Fact]
    public async Task Quote_And_Charge_Round_Identically_At_The_Half_Cent_Boundary()
    {
        // 100.01 × 0.50 = 50.005. The quote must not round the other way from the charge, and the fee
        // is the residual (100.01 − 50.01) rather than a second rounding, so the two always sum to the
        // total the customer sees.
        ArrangeOrder(cleaningInHours: 1, totalPrice: 100.01m);

        await AssertQuoteMatchesChargeAsync(CancellationFeeTier.LastMinute, 0.50m, 50.01m, 50.00m);
    }

    // ── AC5: the preview is a pure read ──

    [Fact]
    public async Task Preview_Writes_Nothing()
    {
        var order = ArrangeOrder(cleaningInHours: 1);
        var statusCountBefore = order.OrderStatusHistory.Count;

        var preview = await PreviewAsync();

        Assert.Equal(0.50m, preview.FeeRate);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(statusCountBefore, order.OrderStatusHistory.Count);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancellationFeeRate);
        Assert.Null(order.CancellationRefundAmount);

        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _loyaltyService.Verify(
            s => s.RevokeForCancelledOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _expressWaiverConsumer.Verify(
            c => c.ReleaseForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _liveActivityProducer.Verify(
            p => p.NotifyOrderTransitionAsync(
                It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<OrderStatusTrack>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _producer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Preview_Can_Be_Called_Repeatedly_Without_Changing_Its_Answer()
    {
        // A read that consumed or reserved anything would drift on the second call. Twelve hours out is
        // far enough from every boundary that the two UtcNow readings cannot cross one.
        ArrangeOrder(cleaningInHours: 12);

        var first = await PreviewAsync();
        var second = await PreviewAsync();
        var third = await PreviewAsync();

        Assert.Equal(first.FeeRate, second.FeeRate);
        Assert.Equal(first.RefundAmount, second.RefundAmount);
        Assert.Equal(first.Tier, third.Tier);
        Assert.Equal(first.FeeRate, third.FeeRate);
    }
}
