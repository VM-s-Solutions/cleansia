using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Owner decision 2026-10-04: once an administrator confirms a lockout, each seat is paid its full contracted
/// reward, from the figures frozen at its take or, on a seat with no contract, the rates in force - always,
/// also when a cash customer never pays the lockout price. The row keeps the lockout line type.
///
/// <para>Frozen at the take: 840 base and 280 for the two extra rooms, floored at 500 and capped at 1 000, so
/// 1 120 is capped to 1 000, and the job was booked heavy at 30 %, adding 300: 1 300 for a lone seat. The
/// rates in force are 600 base and 100 per extra room with no bounds: 600 + 200 = 800, plus 240, 1 040.</para>
/// </summary>
public class LockoutRewardPayTests
{
    private const string OrderId = "order-lockout-reward";
    private const string ServiceId = "svc-lockout-reward";
    private const string CleanerId = "emp-lockout-reward-1";
    private const string SecondCleanerId = "emp-lockout-reward-2";

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigs = new();
    private readonly Mock<IPayPeriodRepository> _payPeriods = new();
    private readonly Mock<IOrderEmployeePayRepository> _pays = new();
    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly List<OrderEmployeePay> _written = [];
    private IReadOnlyList<EmployeePayConfig> _rates = [];

    public LockoutRewardPayTests()
    {
        _payConfigs
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rates);
        _payConfigs
            .Setup(r => r.GetPackageConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _payConfigs
            .Setup(r => r.HasConfigForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rates.Count > 0);
        _payPeriods
            .Setup(r => r.GetActivePeriodAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));
        _receivables.Setup(r => r.GetAll()).Returns(Array.Empty<Receivable>().AsQueryable().BuildMock());
        _pays.Setup(r => r.Add(It.IsAny<OrderEmployeePay>())).Callback<OrderEmployeePay>(_written.Add);
    }

    private static IReadOnlyList<EmployeePayConfig> RatesInForce() =>
        [EmployeePayConfig.CreateForService(ServiceId, 600m, CreateOrderTestData.CurrencyId, extraPerRoom: 100m)];

    private Order ArrangeLockedOutOrder(PaymentType paymentType, PaymentStatus paymentStatus, params string[] crew)
    {
        var currency = CreateOrderTestData.DefaultCurrency();
        var service = Service.Create("cat-1", "Standard clean", "Regular");
        service.Id = ServiceId;
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@example.com",
            customerPhone: "+420123456789",
            customerAddress: Address.Create("Street 1", "Praha", "11000", "cz"),
            rooms: 3,
            bathrooms: 0,
            cleaningDateTime: DateTime.UtcNow.AddHours(-1),
            paymentType: paymentType,
            totalPrice: 3000m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: "customer-lockout-reward",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddSelectedServices([OrderService.Create(order, service, 3000m, 0m, 3000m)]);
        order.UpdateEstimatedTime(crew.Length * 120).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 0m, 0.30m);
        foreach (var employeeId in crew)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(employeeId, ContractStatus.Approved)));
        }

        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, feeRate: BookingPolicy.LockoutFeeRate, refundAmount: 0m,
            reason: OrderCancellationReasons.CustomerLockout);

        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orders.Setup(r => r.GetAll()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orders.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return order;
    }

    private static void FreezeAtTheTake(Order order, string employeeId) =>
        order.AssignedEmployees.Single(oe => oe.EmployeeId == employeeId).FreezeJobPay((840m, 280m, 500m, 1000m));

    private CalculateOrderPay.Validator Validator()
    {
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriods.Setup(r => r.ExistsActivePeriodAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new CalculateOrderPay.Validator(
            _orders.Object,
            employees.Object,
            _payPeriods.Object,
            _payConfigs.Object,
            _pays.Object,
            _receivables.Object,
            Mock.Of<IRefundRepository>(),
            Mock.Of<ICreditAccountRepository>());
    }

    private async Task<OrderEmployeePay> PayAsync(string employeeId)
    {
        var validation = await Validator().ValidateAsync(new CalculateOrderPay.Command(OrderId, employeeId));
        Assert.True(validation.IsValid, string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));

        var handler = new CalculateOrderPay.Handler(
            _orders.Object,
            _payPeriods.Object,
            _payConfigs.Object,
            _pays.Object,
            _receivables.Object,
            Mock.Of<IRefundRepository>(),
            Mock.Of<ICreditAccountRepository>());

        var result = await handler.Handle(new CalculateOrderPay.Command(OrderId, employeeId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return _written.Single(p => p.Id == result.Value!.EmployeePayrollId);
    }

    [Fact]
    public async Task A_Cash_Lockout_Nobody_Paid_For_Pays_The_Seat_Its_Contracted_Reward()
    {
        var order = ArrangeLockedOutOrder(PaymentType.Cash, PaymentStatus.Pending, CleanerId);
        FreezeAtTheTake(order, CleanerId);
        _rates = RatesInForce();

        var pay = await PayAsync(CleanerId);

        Assert.Equal(PayLineType.LockoutFeeShare, pay.LineType);
        Assert.Equal(
            (840m, 280m, 300m, 500m, 1000m, 1300m),
            (pay.BasePay, pay.ExtrasPay, pay.DirtinessPay, pay.MinPay, pay.MaxPay, pay.TotalPay));
        Assert.Equal(CreateOrderTestData.CurrencyId, pay.CurrencyId);
    }

    [Fact]
    public async Task A_Seat_With_No_Contract_Is_Paid_At_The_Rates_In_Force()
    {
        ArrangeLockedOutOrder(PaymentType.Card, PaymentStatus.Paid, CleanerId);
        _rates = RatesInForce();

        var pay = await PayAsync(CleanerId);

        Assert.Equal(PayLineType.LockoutFeeShare, pay.LineType);
        Assert.Equal((600m, 200m, 240m, 1040m), (pay.BasePay, pay.ExtrasPay, pay.DirtinessPay, pay.TotalPay));
    }

    /// <summary>
    /// Two seats frozen at 840 + 280 capped to 1 000, heavy adding 300: each seat is owed half, 420 + 140
    /// clamped to [250, 500] = 500, plus 150, so 650 a seat and the job's 1 300 between them.
    /// </summary>
    [Fact]
    public async Task Each_Seat_Of_A_Crew_Is_Paid_Its_Own_Reward()
    {
        var order = ArrangeLockedOutOrder(PaymentType.Cash, PaymentStatus.Pending, CleanerId, SecondCleanerId);
        FreezeAtTheTake(order, CleanerId);
        FreezeAtTheTake(order, SecondCleanerId);

        var first = await PayAsync(CleanerId);
        var second = await PayAsync(SecondCleanerId);

        Assert.Equal(650m, first.TotalPay);
        Assert.Equal(650m, second.TotalPay);
    }

    [Fact]
    public async Task A_Seat_With_No_Contract_And_No_Rate_Is_Refused()
    {
        ArrangeLockedOutOrder(PaymentType.Cash, PaymentStatus.Pending, CleanerId);
        _rates = [];

        var validation = await Validator().ValidateAsync(new CalculateOrderPay.Command(OrderId, CleanerId));

        Assert.Equal(BusinessErrorMessage.NoPayConfiguration, Assert.Single(validation.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Lockout_Does_Not_Mark_The_Order_As_A_Paid_Out_Completion()
    {
        var order = ArrangeLockedOutOrder(PaymentType.Cash, PaymentStatus.Pending, CleanerId);
        FreezeAtTheTake(order, CleanerId);

        await PayAsync(CleanerId);

        Assert.False(order.EmployeePayCalculated);
    }
}
