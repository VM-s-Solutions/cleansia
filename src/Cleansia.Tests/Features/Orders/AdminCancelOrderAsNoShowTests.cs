using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: an administrator confirms that an assigned cleaner did not
/// arrive, and one action pays the customer exactly what the unfilled sweep pays — the whole refund, the
/// apology credit on the sweep's own key, the no-cleaner reason and message — cancels the order and
/// closes the customer's "service not provided" dispute. A cleaner who did start is not a no-show.
/// </summary>
public class AdminCancelOrderAsNoShowTests
{
    private const string OrderId = "order-no-show-1";
    private const string AdminId = "admin-1";
    private const string CustomerId = "customer-no-show-1";
    private const string CzkId = "czk";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<IRefundRepository> _refundRows = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<ILiveActivityProducer> _liveActivity = new();
    private readonly Mock<IExpressWaiverConsumer> _waiver = ExpressWaiverMocks.NoConsumer();
    private readonly Mock<ILoyaltyService> _loyalty = new();
    private readonly Currency _czk;
    private CreditAccount? _account;

    public AdminCancelOrderAsNoShowTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminId);
        _czk = Currency.Create("CZK", "Kč", "Czech koruna");
        _czk.Id = CzkId;
        _czk.SetNoShowCredit(250m);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest request, CancellationToken _) => BusinessResult.Success(
                new RefundResult("refund-1", $"refund:{request.OrderId}:admin", request.Amount, RefundStatus.Succeeded, false)));
        _credit.Setup(c => c.EnsureForUserAsync(CustomerId, CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _account ??= CreditAccount.Create(CustomerId, CzkId, "system"));
    }

    private AdminCancelOrderAsNoShow.Handler Handler() => new(
        _orders.Object,
        _disputes.Object,
        _session.Object,
        new CleanerNoShowCancellation(
            _credit.Object,
            _refunds.Object,
            _refundRows.Object,
            _notifications.Object,
            new GuestOrderAccessTokenIssuer(Mock.Of<IGuestOrderAccessTokenRepository>()),
            Mock.Of<IPendingDispatch>(),
            NullLogger<CleanerNoShowCancellation>.Instance),
        _notifications.Object,
        _liveActivity.Object,
        _waiver.Object,
        _loyalty.Object,
        TimeProvider.System);

    private Order ArrangeOrder(
        OrderStatus status = OrderStatus.Confirmed,
        double startedMinutesAgo = 40,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid)
    {
        var order = Order.Create(
            customerName: "No Show",
            customerEmail: "no-show@example.test",
            customerPhone: "+420111000222",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-startedMinutesAgo),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: paymentStatus,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(_czk);
        if (paymentType == PaymentType.Card)
        {
            order.AssignStripePaymentIntentId("pi_no_show");
        }

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var track in new[] { OrderStatus.New, status })
        {
            var entry = OrderStatusTrack.Create(track, order);
            entry.Created("test", stamp);
            order.AddOrderStatus(entry);
            stamp = stamp.AddMinutes(1);
        }

        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee("emp-no-show", ContractStatus.Approved)));
        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private Task<BusinessResult<AdminCancelOrderAsNoShow.Response>> ConfirmAsync() =>
        Handler().Handle(new AdminCancelOrderAsNoShow.Command(OrderId), CancellationToken.None);

    [Fact]
    public async Task Confirming_A_No_Show_Cancels_Refunds_In_Full_And_Credits_The_Apology_On_The_Sweeps_Key()
    {
        var order = ArrangeOrder();

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1000m, result.Value!.RefundedAmount);
        Assert.False(result.Value.RefundPending);
        Assert.Equal(250m, result.Value.ApologyCredit);

        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CancelledBy.Admin, order.CancelledBy);
        Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
        Assert.Equal(0m, order.CancellationFeeRate);
        Assert.Equal(1000m, order.CancellationRefundAmount);
        _refunds.Verify(r => r.IssueRefundAsync(
            It.Is<RefundRequest>(q => q.Amount == 1000m && q.Reason == RefundReason.ServiceNotRendered),
            It.IsAny<CancellationToken>()), Times.Once);

        var apology = Assert.Single(_account!.Transactions);
        Assert.Equal($"cleaner-noshow:{OrderId}", apology.IdempotencyKey);
        Assert.Equal(CreditTransactionReason.CleanerNoShow, apology.Reason);

        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderNoCleanerRefunded,
            It.Is<Dictionary<string, string>>(args => args["amount"] == "250 Kč"),
            It.IsAny<string?>(), OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            "emp-no-show-user", NotificationEventCatalog.OrderAssignmentCancelled,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
        _waiver.Verify(w => w.ReleaseForOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _liveActivity.Verify(l => l.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, It.IsAny<OrderStatusTrack>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The customer's credit account sits on the books of a company frozen for archive, so the apology
    /// would fail the cancellation's commit. It is skipped; the refund and the cancellation stand, and the
    /// customer gets the plain cancellation push, which promises no credit.
    /// </summary>
    [Fact]
    public async Task A_Customer_Whose_Credit_Is_On_A_Frozen_Companys_Books_Gets_No_Apology_And_Is_Still_Refunded()
    {
        var order = ArrangeOrder();
        _credit.Setup(c => c.IsOnFrozenCompanyBooksAsync(CustomerId, CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.ApologyCredit);
        Assert.Equal(1000m, result.Value.RefundedAmount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        _credit.Verify(c => c.EnsureForUserAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderCancelled,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task The_Customers_Service_Not_Provided_Dispute_Is_Closed()
    {
        ArrangeOrder();
        var dispute = new Dispute(OrderId, CustomerId, DisputeReason.ServiceNotProvided, "Nobody came to clean.", CustomerId);
        _disputes.Setup(d => d.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(dispute);

        await ConfirmAsync();

        Assert.Equal(DisputeStatus.Closed, dispute.Status);
    }

    [Fact]
    public async Task A_Dispute_About_Something_Else_Is_Left_For_The_Administrator()
    {
        ArrangeOrder();
        var dispute = new Dispute(OrderId, CustomerId, DisputeReason.DamagedProperty, "A vase broke last week.", CustomerId);
        _disputes.Setup(d => d.GetOpenDisputeForOrderAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(dispute);

        await ConfirmAsync();

        Assert.Equal(DisputeStatus.Pending, dispute.Status);
    }

    /// <summary>
    /// The race the report opens: the customer reports, then the cleaner taps Start. A cleaner who
    /// started is not a no-show, so the confirmation refuses and moves no money.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Who_Started_After_The_Report_Is_Not_A_No_Show()
    {
        var order = ArrangeOrder(status: OrderStatus.InProgress);

        var result = await ConfirmAsync();

        Assert.Equal(BusinessErrorMessage.OrderCleanerAlreadyStarted, result.Error!.Message);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        _refunds.Verify(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Before_The_Booked_Start_There_Is_No_No_Show_To_Confirm()
    {
        ArrangeOrder(startedMinutesAgo: -30);

        var result = await ConfirmAsync();

        Assert.Equal(BusinessErrorMessage.OrderStartTimeNotReached, result.Error!.Message);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, BusinessErrorMessage.OrderAlreadyCancelled)]
    [InlineData(OrderStatus.Completed, BusinessErrorMessage.OrderAlreadyCompleted)]
    public async Task A_Finished_Order_Is_Refused(OrderStatus status, string expected)
    {
        ArrangeOrder(status: status);

        var result = await ConfirmAsync();

        Assert.Equal(expected, result.Error!.Message);
    }

    [Fact]
    public async Task A_Refund_Stripe_Refuses_Is_Pending_And_The_Customer_Is_Told_So()
    {
        ArrangeOrder();
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.RefundedAmount);
        Assert.True(result.Value.RefundPending);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderNoCleanerRefundPending,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Cash_Booking_Is_Told_Nothing_Was_Charged()
    {
        ArrangeOrder(paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);

        var result = await ConfirmAsync();

        Assert.False(result.Value!.RefundPending);
        _refunds.Verify(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderNoCleanerNothingCharged,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 1000 paid 700 by card and 300 in credit, with 200 already settled in credit. Stripe gave no answer on
    /// the 560 card refund, so it waits for the re-drive, but it may already have gone through: of the 800
    /// left, 240 of credit comes back now, not all 300, and the customer can never end with more than 1000.
    /// </summary>
    [Fact]
    public async Task A_Card_Refund_Left_Pending_Counts_As_Given_Back_When_The_Credit_Comes_Back_Now()
    {
        var order = ArrangeOrder();
        order.ApplyCredit(300m, CustomerId);
        _refunds.Setup(r => r.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));
        _refundRows.Setup(r => r.GetByRefundKeyAsync($"refund:{OrderId}:admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Refund.Create(
                OrderId, $"refund:{OrderId}:admin", 560m, "CZK", RefundReason.ServiceNotRendered, RefundSource.AppRefund));
        _credit.Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200m);

        var result = await ConfirmAsync();

        Assert.True(result.Value!.RefundPending);
        _credit.Verify(c => c.TryReturnAsync(
            CustomerId, CzkId, 240m, $"credit-return:order-ended-unpaid:{OrderId}", AdminId,
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }

    /// <summary>
    /// The order was partly refunded, 560 by card and 240 in credit of 800, and a complaint was settled in 150
    /// of credit. Of the 1000 sale 50 is left, so 50 of the 60 credit still out comes back, not all 60.
    /// </summary>
    [Fact]
    public async Task A_Partly_Refunded_Order_Gets_Back_No_More_Credit_Than_The_Sale_Has_Left_After_Its_Card_Refunds()
    {
        var order = ArrangeOrder(paymentStatus: PaymentStatus.PartiallyRefunded);
        order.ApplyCredit(300m, CustomerId);
        _refundRows.Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(560m);
        _credit.Setup(c => c.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(240m);
        _credit.Setup(c => c.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(150m);

        await ConfirmAsync();

        _credit.Verify(c => c.TryReturnAsync(
            CustomerId, CzkId, 50m, $"credit-return:order-ended-unpaid:{OrderId}", AdminId,
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
    }
}
