using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Infrastructure;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28, decisions 11 and 13: an administrator confirms the cleaner's lockout report,
/// also on a job already started at the door, and the booking is cancelled as the customer's at the whole
/// price. A card order keeps its payment and the credit applied to it; a signed-in customer's unpaid cash
/// booking gets its credit back and owes the price as a lockout receivable; a guest keeps nothing back and
/// owes nothing more. Without a report there is nothing to confirm.
/// </summary>
public class AdminCancelOrderAsLockoutTests
{
    private const string OrderId = "order-lockout-2";
    private const string AdminId = "admin-1";
    private const string CustomerId = "customer-lockout-2";
    private const string EmployeeId = "emp-lockout-2";
    private const string TenantId = "cleansia-cz";
    private const string CzkId = "czk";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<ILiveActivityProducer> _liveActivity = new();
    private readonly Mock<ILoyaltyService> _loyalty = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<IAuditContext> _audit = new();
    private readonly List<Receivable> _opened = [];
    private readonly Currency _czk;

    public AdminCancelOrderAsLockoutTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminId);
        _receivables.Setup(r => r.Add(It.IsAny<Receivable>())).Callback<Receivable>(_opened.Add);
        _czk = Currency.Create("CZK", "Kč", "Czech koruna");
        _czk.Id = CzkId;
    }

    private AdminCancelOrderAsLockout.Handler Handler() => new(
        _orders.Object,
        _receivables.Object,
        _credit.Object,
        _session.Object,
        _notifications.Object,
        _liveActivity.Object,
        _loyalty.Object,
        TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
        _pending.Object,
        _audit.Object,
        TimeProvider.System);

    private Order ArrangeOrder(
        OrderStatus status = OrderStatus.InProgress,
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string? userId = CustomerId,
        decimal creditApplied = 0m,
        bool reported = true)
    {
        var order = Order.Create(
            customerName: "Locked Out",
            customerEmail: "locked-out@example.test",
            customerPhone: "+420111000444",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-40),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: paymentStatus,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.TenantId = TenantId;
        order.SetCurrency(_czk);
        if (paymentType == PaymentType.Card)
        {
            order.AssignStripePaymentIntentId("pi_lockout");
        }
        if (creditApplied > 0m)
        {
            order.ApplyCredit(creditApplied, CustomerId);
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
            order, ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved)));
        if (reported)
        {
            order.ReportLockout(EmployeeId, "Called three times, no answer.", DateTime.UtcNow.AddMinutes(-20));
        }

        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private Task<BusinessResult<AdminCancelOrderAsLockout.Response>> ConfirmAsync() =>
        Handler().Handle(new AdminCancelOrderAsLockout.Command(OrderId), CancellationToken.None);

    [Fact]
    public async Task A_Started_Card_Order_Is_Cancelled_At_The_Whole_Price_And_Keeps_Its_Payment_And_Credit()
    {
        var order = ArrangeOrder(creditApplied: 200m);

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1000m, result.Value!.FeeAmount);
        Assert.Null(result.Value.ReceivableAmount);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CancelledBy.Admin, order.CancelledBy);
        Assert.Equal(OrderCancellationReasons.CustomerLockout, order.CancellationReason);
        Assert.Equal(1m, order.CancellationFeeRate);
        Assert.Equal(0m, order.CancellationRefundAmount);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Empty(_opened);
        _credit.Verify(c => c.TryReturnAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);

        _notifications.Verify(n => n.NotifyAsync(
            CustomerId, NotificationEventCatalog.OrderCancelled,
            It.Is<Dictionary<string, string>>(args => args["orderId"] == OrderId),
            TenantId, OrderId, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyAsync(
            EmployeeId + "-user", NotificationEventCatalog.OrderAssignmentCancelled,
            It.IsAny<Dictionary<string, string>>(), It.IsAny<string?>(), OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendOrderLockoutEmailMessage>>(e => e.Payload.OrderId == OrderId && e.TenantId == TenantId),
            MessageKeys.OrderLockoutEmail(OrderId)), Times.Once);
        _liveActivity.Verify(l => l.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, It.IsAny<OrderStatusTrack>(), It.IsAny<CancellationToken>()), Times.Once);
        _loyalty.Verify(l => l.RevokeForCancelledOrderAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Unpaid_Cash_Booking_Gets_Its_Credit_Back_And_Owes_The_Whole_Price_As_A_Lockout_Receivable()
    {
        var order = ArrangeOrder(
            status: OrderStatus.Confirmed, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending,
            creditApplied: 200m);

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1000m, result.Value!.ReceivableAmount);
        var receivable = Assert.Single(_opened);
        Assert.Equal(ReceivableKind.Lockout, receivable.Kind);
        Assert.Equal(ReceivableStatus.Open, receivable.Status);
        Assert.Equal(1000m, receivable.Amount);
        Assert.Equal((OrderId, CustomerId, CzkId), (receivable.OrderId, receivable.UserId, receivable.CurrencyId));
        _credit.Verify(c => c.TryReturnAsync(
            CustomerId, CzkId, 200m, $"credit-return:order-ended-unpaid:{OrderId}", AdminId,
            It.IsAny<CancellationToken>(), OrderId, It.IsAny<string?>()), Times.Once);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
    }

    /// <summary>
    /// Owner decision 2026-10-04: the crew is paid its reward once the lockout is confirmed, whether the
    /// payment was kept, the price is still owed on a receivable, or a guest's cash booking owes nothing.
    /// </summary>
    [Theory]
    [InlineData(PaymentType.Card, PaymentStatus.Paid, CustomerId)]
    [InlineData(PaymentType.Cash, PaymentStatus.Pending, CustomerId)]
    [InlineData(PaymentType.Cash, PaymentStatus.Pending, null)]
    public async Task The_Confirmation_Asks_For_The_Crews_Reward_At_Once(
        PaymentType paymentType, PaymentStatus paymentStatus, string? userId)
    {
        ArrangeOrder(status: OrderStatus.Confirmed, paymentType: paymentType, paymentStatus: paymentStatus, userId: userId);

        await ConfirmAsync();

        _pending.Verify(p => p.Enqueue(
            QueueNames.CalculateOrderPay,
            It.Is<QueueEnvelope<CalculateOrderPayMessage>>(e => e.TenantId == TenantId
                && e.Payload.OrderId == OrderId && e.Payload.EmployeeId == EmployeeId),
            MessageKeys.Pay(OrderId, EmployeeId)), Times.Once);
    }

    [Fact]
    public async Task A_Guest_Keeps_Nothing_Back_Owes_Nothing_More_And_Is_Told_By_The_Guest_Cancellation_Email()
    {
        var order = ArrangeOrder(userId: null);

        var result = await ConfirmAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.ReceivableAmount);
        Assert.Empty(_opened);
        Assert.Equal(0m, order.CancellationRefundAmount);
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendGuestOrderCancellationEmailMessage>>(e =>
                e.Payload.OrderId == OrderId && e.Payload.SuccessfulRefundAmount == null),
            MessageKeys.GuestOrderCancelledEmail(OrderId)), Times.Once);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(), It.IsAny<QueueEnvelope<SendOrderLockoutEmailMessage>>(), It.IsAny<string>()),
            Times.Never);
        _notifications.Verify(n => n.NotifyAsync(
            It.IsAny<string>(), NotificationEventCatalog.OrderCancelled, It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_Audit_Row_Records_The_Report_And_The_Money_Before_And_After()
    {
        ArrangeOrder(status: OrderStatus.OnTheWay, paymentType: PaymentType.Cash, paymentStatus: PaymentStatus.Pending);

        await ConfirmAsync();

        _audit.Verify(a => a.RecordChange(
            "Order",
            OrderId,
            It.Is<AdminCancelOrderAsLockout.LockoutSnapshot>(s =>
                s.Status == OrderStatus.OnTheWay && s.CancellationFeeRate == null && s.ReceivableAmount == null
                && s.LockoutReportedByEmployeeId == EmployeeId && s.LockoutReportedAt != null),
            It.Is<AdminCancelOrderAsLockout.LockoutSnapshot>(s =>
                s.Status == OrderStatus.Cancelled && s.CancellationFeeRate == 1m && s.ReceivableAmount == 1000m
                && s.LockoutReportedByEmployeeId == EmployeeId),
            null), Times.Once);
    }

    [Fact]
    public async Task Without_The_Cleaners_Report_There_Is_No_Lockout_To_Confirm()
    {
        var order = ArrangeOrder(reported: false);

        var result = await ConfirmAsync();

        Assert.Equal(BusinessErrorMessage.LockoutNotReported, result.Error!.Message);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        Assert.Null(order.CancellationFeeRate);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, BusinessErrorMessage.OrderAlreadyCancelled)]
    [InlineData(OrderStatus.Completed, BusinessErrorMessage.OrderAlreadyCompleted)]
    public async Task A_Finished_Order_Is_Refused(OrderStatus status, string expected)
    {
        ArrangeOrder(status: status);

        var result = await ConfirmAsync();

        Assert.Equal(expected, result.Error!.Message);
        Assert.Empty(_opened);
    }
}
