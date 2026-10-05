using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-06: the assigned cleaner of a signed-in customer's cash order in progress reports that
/// the customer did not pay at the door. The job completes, the price less any credit applied becomes an
/// open unpaid-cash receivable, the cleaner is paid as for any completed job, the customer is told by push
/// and e-mail and the company's administrators are alerted. No receipt is issued, because no money arrived.
/// A cleaner not on the job is answered as if the order did not exist; every other refusal says why.
/// </summary>
public sealed class ReportCashNotPaidTests
{
    private const string OrderId = "order-cash-not-paid";
    private const string EmployeeId = "emp-cash-not-paid";
    private const string CustomerId = "user-cash-not-paid";
    private const string TenantId = "cleansia-cz";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IOrderPhotoRepository> _photos = new();
    private readonly Mock<IOrderAccessService> _access = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<INotificationProducer> _notifications = new();
    private readonly Mock<ILiveActivityProducer> _liveActivities = new();
    private readonly Mock<IAdminNotifier> _adminNotifier = new();
    private Mock<IWorkContractAcceptanceRepository> _acceptances = WorkContractTestData.AcceptanceRepository();

    private Order ArrangeOrder(
        PaymentType paymentType = PaymentType.Cash,
        PaymentStatus paymentStatus = PaymentStatus.Pending,
        OrderStatus status = OrderStatus.InProgress,
        string? userId = CustomerId,
        ContractStatus cleaner = ContractStatus.Approved,
        bool cleanerProfileComplete = true,
        int afterPhotos = 1,
        string callerEmployeeId = EmployeeId)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, status, EmployeeId, paymentType, paymentStatus,
            cleaningDateTime: DateTime.UtcNow.AddHours(-2), userId: userId);
        order.TenantId = TenantId;
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, cleaner, withAddress: cleanerProfileComplete);

        _orders.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employees.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());
        _photos
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(afterPhotos);
        _access.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(callerEmployeeId);
        return order;
    }

    private ReportCashNotPaid.Validator Validator() => new(
        _orders.Object, _employees.Object, _photos.Object, _access.Object, _acceptances.Object);

    private ReportCashNotPaid.Handler Handler() => new(
        _orders.Object, _receivables.Object, _pending.Object, _notifications.Object, _liveActivities.Object, _adminNotifier.Object);

    [Fact]
    public async Task The_Assigned_Approved_Cleaner_Of_A_Cash_Order_In_Progress_May_Report_It()
    {
        ArrangeOrder();

        var result = await Validator().ValidateAsync(new ReportCashNotPaid.Command(OrderId));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    public static TheoryData<string, string> Refusals => new()
    {
        { "card order", BusinessErrorMessage.OrderCashNotAllowedOnCardOrder },
        { "cash already collected", BusinessErrorMessage.OrderCashAlreadyCollected },
        { "payment refunded", BusinessErrorMessage.OrderPaymentNotOutstanding },
        { "not in progress", BusinessErrorMessage.OrderNotInProgress },
        { "guest order", BusinessErrorMessage.OrderPaymentNotOutstanding },
        { "caller off the crew", BusinessErrorMessage.OrderNotFound },
        { "no after photo", BusinessErrorMessage.AfterPhotosRequired },
        { "unapproved cleaner", BusinessErrorMessage.EmployeeNotApproved },
        { "no contract for the seat", BusinessErrorMessage.WorkContractAcceptanceRequired },
        { "incomplete profile", BusinessErrorMessage.EmployeeProfileIncomplete },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_Report_That_Cannot_Stand_Is_Refused_With_One_Reason(string situation, string error)
    {
        switch (situation)
        {
            case "card order": ArrangeOrder(paymentType: PaymentType.Card); break;
            case "cash already collected": ArrangeOrder(paymentStatus: PaymentStatus.Paid); break;
            case "payment refunded": ArrangeOrder(paymentStatus: PaymentStatus.Refunded); break;
            case "not in progress": ArrangeOrder(status: OrderStatus.Confirmed); break;
            case "guest order": ArrangeOrder(userId: null); break;
            case "caller off the crew": ArrangeOrder(callerEmployeeId: "emp-somebody-else"); break;
            case "no after photo": ArrangeOrder(afterPhotos: 0); break;
            case "unapproved cleaner": ArrangeOrder(cleaner: ContractStatus.Rejected); break;
            case "no contract for the seat":
                _acceptances = WorkContractTestData.AcceptanceRepository(everySeatAccepted: false);
                ArrangeOrder();
                break;
            case "incomplete profile": ArrangeOrder(cleanerProfileComplete: false); break;
            default: throw new ArgumentOutOfRangeException(nameof(situation), situation, null);
        }

        var result = await Validator().ValidateAsync(new ReportCashNotPaid.Command(OrderId));

        Assert.Equal(error, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Job_Completes_Unpaid_And_The_Price_Less_The_Credit_Applied_Is_Owed()
    {
        var order = ArrangeOrder();
        order.ApplyCredit(200m, CustomerId);
        Receivable? opened = null;
        _receivables.Setup(r => r.Add(It.IsAny<Receivable>())).Callback<Receivable>(r => opened = r);

        var result = await Handler().Handle(new ReportCashNotPaid.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new ReportCashNotPaid.Response(OrderId, OrderStatus.Completed, 800m), result.Value);
        Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
        Assert.NotNull(order.CompletedAt);
        Assert.True(order.ActualCompletionTime > 0);
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.Null(order.CashCollectedAt);

        Assert.NotNull(opened);
        Assert.Equal(
            (ReceivableKind.UnpaidCash, ReceivableStatus.Open, 800m, OrderId, CustomerId, order.CurrencyId),
            (opened!.Kind, opened.Status, opened.Amount, opened.OrderId, opened.UserId, opened.CurrencyId));
        _receivables.Verify(r => r.Add(It.IsAny<Receivable>()), Times.Once);
    }

    [Fact]
    public async Task The_Cleaner_Is_Paid_As_For_A_Completed_Job_And_No_Receipt_Is_Issued()
    {
        ArrangeOrder();

        await Handler().Handle(new ReportCashNotPaid.Command(OrderId), CancellationToken.None);

        _pending.Verify(p => p.Enqueue(
            QueueNames.CalculateOrderPay,
            It.Is<QueueEnvelope<CalculateOrderPayMessage>>(e => e.TenantId == TenantId
                && e.Payload.OrderId == OrderId && e.Payload.EmployeeId == EmployeeId),
            MessageKeys.Pay(OrderId, EmployeeId)), Times.Once);
        _pending.Verify(p => p.Enqueue(QueueNames.GenerateReceipt, It.IsAny<It.IsAnyType>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task The_Customer_Is_Told_By_Push_And_E_Mail_And_The_Administrators_Once()
    {
        var order = ArrangeOrder();
        Receivable? opened = null;
        _receivables.Setup(r => r.Add(It.IsAny<Receivable>())).Callback<Receivable>(r => opened = r);

        await Handler().Handle(new ReportCashNotPaid.Command(OrderId), CancellationToken.None);

        var amount = MoneyText.Format(1000m, order.Currency!);
        _notifications.Verify(n => n.NotifyAsync(
            CustomerId,
            NotificationEventCatalog.OrderCashNotPaid,
            It.Is<Dictionary<string, string>>(a => a["orderId"] == OrderId
                && a["orderNumber"] == order.DisplayOrderNumber && a["amount"] == amount),
            TenantId,
            opened!.Id,
            It.IsAny<CancellationToken>()), Times.Once);
        _pending.Verify(p => p.Enqueue(
            QueueNames.SendEmail,
            It.Is<QueueEnvelope<SendOrderCashNotPaidEmailMessage>>(e => e.TenantId == TenantId
                && e.Payload.ReceivableId == opened!.Id),
            MessageKeys.OrderCashNotPaidEmail(opened!.Id)), Times.Once);
        _adminNotifier.Verify(n => n.NotifyAsync(
            It.Is<AdminEvent>(e => e.Key == AdminNotificationEventCatalog.OrderCashNotPaid
                && e.TenantId == TenantId
                && e.Subject == OrderId
                && e.Args["orderNumber"] == order.DisplayOrderNumber
                && e.Args["amount"] == amount
                && e.Args["orderId"] == OrderId),
            It.IsAny<CancellationToken>()), Times.Once);
        _liveActivities.Verify(l => l.NotifyOrderTransitionAsync(
            order, LiveActivityEventKeys.End, It.Is<OrderStatusTrack>(t => t.Status == OrderStatus.Completed),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
