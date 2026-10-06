using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MockQueryable;
using Cleansia.Tests.Common;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using Cleansia.Core.Queue.Abstractions;

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
///
/// T-0525: the fixture used to model "accepted" as an <c>OrderStatus.Confirmed</c> track alone, which
/// is what the payment webhook writes for EVERY card order — so as written these cases asserted the
/// defect (a customer billed 25%/50% for a job no cleaner had seen) while intending to assert the tier
/// ladder. The fixture now carries the real acceptance signal, an assignment row; the tier assertions
/// are unchanged, which is the point — the ladder itself did not move.
/// </summary>
public class CancelOrderStandardTierFeeTests
{
    private const string OrderId = "order-std-1";
    private const string UserId = "user-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<ILoyaltyService> _loyaltyService = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ILiveActivityProducer> _liveActivityProducer = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();

    public CancelOrderStandardTierFeeTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        // No active membership → the resolver returns the standard absolute 24h window, the value the
        // handler passes as freeCancellationHoursOverride for every non-member.
        _membershipRepository
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false)));
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
                new CancellationPolicyResolver(_membershipRepository.Object, Mock.Of<IOrderRepository>()),
                _producer.Object,
                _liveActivityProducer.Object,
                _expressWaiverConsumer.Object,
                Mock.Of<IPendingDispatch>(),
                new AuditContext(),
                TimeProvider.System,
                NullLogger<CustomerOrderCancellation>.Instance));

    private Order ArrangeAcceptedCardPaidOrder(DateTime cleaningUtc, decimal totalPrice, decimal creditApplied = 0m)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningUtc,
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        // Created well before the oops window so the short-circuit cannot mask the tier.
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);
        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, UserId);
        }

        order.AssignStripeSessionId("cs_test_std");

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("tester", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        var cleanerUser = User.CreateWithPassword("cleaner@cleansia.test", "Passw0rd!", "Clean", "Er");
        cleanerUser.Id = "emp-std-user";
        var cleaner = Employee.CreateWithUser(cleanerUser);
        cleaner.Id = "emp-std";
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetAll())
            .Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private void AlreadyGivenBack(decimal cardSucceeded = 0m, decimal cardPending = 0m, decimal creditReturned = 0m)
    {
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardSucceeded);
        _refundRepository
            .Setup(r => r.GetPendingRefundTotalForOrderAsync(OrderId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cardPending);
        _creditAccountRepository
            .Setup(r => r.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(creditReturned);
    }

    private List<RefundRequest> CaptureIssuedRefunds()
    {
        var issued = new List<RefundRequest>();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
            {
                issued.Add(req);
                return BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false));
            });
        return issued;
    }

    private async Task<OrderEmployeePay> CrewPayAsync(Order order)
    {
        var payPeriods = new Mock<IPayPeriodRepository>();
        payPeriods
            .Setup(r => r.GetActivePeriodAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));
        var written = new List<OrderEmployeePay>();
        var pays = new Mock<IOrderEmployeePayRepository>();
        pays.Setup(r => r.Add(It.IsAny<OrderEmployeePay>())).Callback<OrderEmployeePay>(written.Add);
        var handler = new CalculateOrderPay.Handler(
            _orderRepository.Object,
            payPeriods.Object,
            Mock.Of<IEmployeePayConfigRepository>(),
            pays.Object,
            Mock.Of<IReceivableRepository>(),
            _refundRepository.Object,
            _creditAccountRepository.Object,
            Mock.Of<IAppConfigurationProvider>());

        var employeeId = order.AssignedEmployees.Single().EmployeeId;
        var result = await handler.Handle(new CalculateOrderPay.Command(OrderId, employeeId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return Assert.Single(written);
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

    /// <summary>
    /// 1000 by card, 400 of it already refunded. The fee is a share of the price, so the cancellation returns
    /// the 600 still held less the fee: 350 at 25 %, 100 at 50 % (owner ruling 2026-10-06).
    /// </summary>
    [Theory]
    [InlineData(12, 350)]
    [InlineData(1, 100)]
    public async Task A_Fee_Cancellation_Of_A_Partly_Refunded_Card_Order_Returns_What_Is_Held_Less_The_Fee(
        int hoursBeforeStart, int expected)
    {
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(hoursBeforeStart), totalPrice: 1000m);
        order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
        AlreadyGivenBack(cardSucceeded: 400m);
        var issued = CaptureIssuedRefunds();

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.RefundInitiated);
        Assert.Equal(expected, result.Value.RefundAmount);
        Assert.Equal(expected, order.CancellationRefundAmount);
        Assert.Equal(expected, Assert.Single(issued).Amount);
    }

    /// <summary>
    /// What already came back reaches the price less the 250 fee, so nothing more is returned: neither the
    /// card nor the 60 of credit still out on the card-and-credit order.
    /// </summary>
    [Theory]
    [InlineData(800, 0, 0)]
    [InlineData(750, 0, 0)]
    [InlineData(560, 300, 240)]
    public async Task A_Fee_Cancellation_Of_An_Order_Already_Refunded_Past_The_Price_Less_The_Fee_Returns_Nothing(
        int cardRefunded, int creditApplied, int creditReturned)
    {
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(12), totalPrice: 1000m, creditApplied);
        order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
        AlreadyGivenBack(cardSucceeded: cardRefunded, creditReturned: creditReturned);

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(BookingPolicy.PartialCancellationFeeRate, result.Value!.FeeRate);
        Assert.Equal(0m, result.Value.RefundAmount);
        Assert.False(result.Value.RefundInitiated);
        Assert.False(result.Value.RefundPending);
        Assert.Null(result.Value.ActualRefundAmount);
        Assert.Equal(0m, order.CancellationRefundAmount);
        _refundService.Verify(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _creditAccountRepository.Verify(r => r.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// An earlier refund is still pending on a Paid order: 280 of card, and on the card-and-credit order its
    /// 120 credit leg, which waits for that card. Both count as given back, so 1000 − 400 − 250 = 350 is
    /// returned and the customer has 750 once the pending refund goes through.
    /// </summary>
    [Theory]
    [InlineData(300, 280)]
    [InlineData(0, 400)]
    public async Task A_Fee_Cancellation_Beside_A_Pending_Refund_Counts_Its_Card_And_Its_Credit_Leg(
        int creditApplied, int pendingCard)
    {
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(12), totalPrice: 1000m, creditApplied);
        AlreadyGivenBack(cardPending: pendingCard);
        var issued = CaptureIssuedRefunds();

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(350m, result.Value!.RefundAmount);
        Assert.Equal(350m, order.CancellationRefundAmount);
        Assert.Equal(350m, Assert.Single(issued).Amount);
    }

    /// <summary>
    /// The 350 of a cancellation that left 400 already refunded is still pending at Stripe, so the company
    /// holds 600 for now; the crew is paid on the 250 fee it keeps, not on the 600.
    /// </summary>
    [Fact]
    public async Task A_Fee_Cancellation_Whose_Own_Refund_Is_Pending_Pays_The_Crew_On_The_Fee_Kept()
    {
        var order = ArrangeAcceptedCardPaidOrder(DateTime.UtcNow.AddHours(12), totalPrice: 1000m);
        order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);
        AlreadyGivenBack(cardSucceeded: 400m);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(new CancelOrder.Command(OrderId, null), CancellationToken.None);
        var pay = await CrewPayAsync(order);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.RefundPending);
        Assert.Equal(350m, order.CancellationRefundAmount);
        Assert.Equal(PayLineType.CancellationFeeShare, pay.LineType);
        Assert.Equal(125m, pay.TotalPay);
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
