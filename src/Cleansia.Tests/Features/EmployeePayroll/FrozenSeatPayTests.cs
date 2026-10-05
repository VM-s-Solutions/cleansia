using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Tests.Features.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// Owner ruling 2026-10-03: a cleaner's pay is frozen when the contract for work forms. The seat keeps the
/// job figures its reward was priced from and is paid from them, so a rate edit after the take reprices only
/// jobs taken after it; a seat with no contract yet is still paid at the rates in force.
///
/// <para>The rates at the take: 840 base and 140 per extra room, floored at 500 and capped at 1 000. The job
/// has 3 rooms, so 840 + 2 x 140 = 1 120, capped to 1 000; it was booked heavy at 30 %, adding 300, so one
/// seat earns 1 300. After the take the cleaner is re-graded to 600 base and 100 per extra room with no
/// bounds: 600 + 2 x 100 = 800, plus 240 for heavy, 1 040.</para>
/// </summary>
public class FrozenSeatPayTests
{
    private const string OrderId = "order-frozen-pay";
    private const string ServiceId = "svc-frozen-pay";
    private const string CleanerId = "emp-frozen-pay-1";
    private const string SecondCleanerId = "emp-frozen-pay-2";
    private const decimal BookedHeavyRate = 0.30m;

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigs = new();
    private readonly Mock<IPayPeriodRepository> _payPeriods = new();
    private readonly Mock<IOrderEmployeePayRepository> _pays = new();
    private readonly Mock<IAppConfigurationProvider> _configuration = new();
    private readonly List<OrderEmployeePay> _written = [];
    private IReadOnlyList<EmployeePayConfig> _rates = [];
    private string? _extrasSharePercent;

    public FrozenSeatPayTests()
    {
        _payConfigs
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _rates);
        _payConfigs
            .Setup(r => r.GetPackageConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _payPeriods
            .Setup(r => r.GetActivePeriodAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayPeriod.CreateBiWeekly(DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));
        _pays.Setup(r => r.Add(It.IsAny<OrderEmployeePay>())).Callback<OrderEmployeePay>(_written.Add);
        _configuration
            .Setup(c => c.GetTenantSettingAsync(TenantSettingCatalog.ExtrasSharePercentKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _extrasSharePercent);
    }

    private static IReadOnlyList<EmployeePayConfig> RatesAtTheTake()
    {
        var config = EmployeePayConfig.CreateForService(ServiceId, 840m, CreateOrderTestData.CurrencyId, extraPerRoom: 140m);
        config.SetPayLimits(500m, 1000m);
        return [config];
    }

    private static IReadOnlyList<EmployeePayConfig> RatesAfterTheRegrade() =>
        [EmployeePayConfig.CreateForService(ServiceId, 600m, CreateOrderTestData.CurrencyId, extraPerRoom: 100m)];

    private Order ArrangeCompletedOrder(decimal dirtinessRate, params string[] crew)
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
            cleaningDateTime: new DateTime(2026, 11, 30, 9, 0, 0, DateTimeKind.Utc),
            paymentType: PaymentType.Card,
            totalPrice: 3000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.AddSelectedServices([OrderService.Create(order, service, 3000m, 0m, 3000m)]);
        order.UpdateEstimatedTime(crew.Length * 120).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 0m, dirtinessRate);
        foreach (var employeeId in crew)
        {
            order.AddAssignedEmployee(OrderEmployee.Create(
                order, ValidatorTestHelpers.BuildEmployee(employeeId, ContractStatus.Approved)));
        }

        _orders.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orders.Setup(r => r.GetAll()).Returns(new[] { order }.AsQueryable().BuildMock());
        return order;
    }

    private static OrderEmployee Seat(Order order, string employeeId) =>
        order.AssignedEmployees.Single(oe => oe.EmployeeId == employeeId);

    /// <summary>What the acceptor does at the take: price the contract and freeze its figures on the seat.</summary>
    private async Task<decimal> TakeAsync(Order order, string employeeId)
    {
        var (facts, jobPay) = (await new WorkContractFactsBuilder(_orders.Object, _payConfigs.Object, _configuration.Object)
            .BuildAsync(OrderId, employeeId, CancellationToken.None))!.Value;
        Seat(order, employeeId).FreezeJobPay(jobPay!.Value);
        return facts.TotalPrice;
    }

    private async Task<OrderEmployeePay> PayAsync(string employeeId)
    {
        var handler = new CalculateOrderPay.Handler(
            _orders.Object,
            _payPeriods.Object,
            _payConfigs.Object,
            _pays.Object,
            Mock.Of<IReceivableRepository>(),
            Mock.Of<IRefundRepository>(),
            Mock.Of<ICreditAccountRepository>(),
            _configuration.Object);

        var result = await handler.Handle(new CalculateOrderPay.Command(OrderId, employeeId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return _written.Single(p => p.Id == result.Value!.EmployeePayrollId);
    }

    [Fact]
    public async Task A_Rate_Edit_After_The_Take_Does_Not_Reprice_The_Seat()
    {
        var order = ArrangeCompletedOrder(BookedHeavyRate, CleanerId);
        _rates = RatesAtTheTake();
        await TakeAsync(order, CleanerId);

        _rates = RatesAfterTheRegrade();
        var pay = await PayAsync(CleanerId);

        Assert.Equal(840m, pay.BasePay);
        Assert.Equal(280m, pay.ExtrasPay);
        Assert.Equal(300m, pay.DirtinessPay);
        Assert.Equal(500m, pay.MinPay);
        Assert.Equal(1000m, pay.MaxPay);
        Assert.Equal(1300m, pay.TotalPay);
    }

    [Fact]
    public async Task A_Lone_Seat_Is_Paid_Exactly_The_Reward_Its_Contract_States()
    {
        var order = ArrangeCompletedOrder(BookedHeavyRate, CleanerId);
        _rates = RatesAtTheTake();
        var contractReward = await TakeAsync(order, CleanerId);

        _rates = RatesAfterTheRegrade();
        var pay = await PayAsync(CleanerId);

        Assert.Equal(1300m, contractReward);
        Assert.Equal(contractReward, pay.TotalPay);
    }

    /// <summary>
    /// A two-seat job priced at 100.21 and booked increased at 15 % adds 15.03 (15.0315 rounded). Each seat
    /// is owed 50.10 and 7.51; the first also takes the cent residue of both terms, 50.11 and 7.52, so the
    /// rows add up to the job's 115.24. The re-grade would pay 400 + 60 a seat.
    /// </summary>
    [Fact]
    public async Task The_First_Seat_Still_Takes_The_Cent_Residue()
    {
        var order = ArrangeCompletedOrder(0.15m, CleanerId, SecondCleanerId);
        foreach (var seat in order.AssignedEmployees)
        {
            seat.FreezeJobPay((100.21m, 0m, 0m, 0m));
        }

        _rates = RatesAfterTheRegrade();
        var first = await PayAsync(CleanerId);
        var second = await PayAsync(SecondCleanerId);

        Assert.Equal((50.11m, 7.52m, 57.63m), (first.BasePay, first.DirtinessPay, first.TotalPay));
        Assert.Equal((50.10m, 7.51m, 57.61m), (second.BasePay, second.DirtinessPay, second.TotalPay));
        Assert.Equal(115.24m, first.TotalPay + second.TotalPay);
    }

    [Fact]
    public async Task A_Seat_With_No_Contract_Is_Paid_At_The_Rates_In_Force()
    {
        ArrangeCompletedOrder(BookedHeavyRate, CleanerId);

        _rates = RatesAfterTheRegrade();
        var pay = await PayAsync(CleanerId);

        Assert.Equal((600m, 200m, 240m, 1040m), (pay.BasePay, pay.ExtrasPay, pay.DirtinessPay, pay.TotalPay));
    }

    /// <summary>
    /// The row keeps the frozen parts, so a 200 charge re-clamps 840 + 280 to the 1 000 cap as at
    /// calculation and leaves 1 000 + 300 - 200 = 1 100.
    /// </summary>
    [Fact]
    public async Task A_Dispute_Charge_Re_Clamps_The_Frozen_Core_As_It_Was_Clamped()
    {
        var order = ArrangeCompletedOrder(BookedHeavyRate, CleanerId);
        _rates = RatesAtTheTake();
        await TakeAsync(order, CleanerId);
        _rates = RatesAfterTheRegrade();
        var pay = await PayAsync(CleanerId);

        pay.ChargeForDispute("dispute-frozen-pay", 200m, "Damaged a vase");

        Assert.Equal(1100m, pay.TotalPay);
    }

    /// <summary>
    /// Owner decision 2026-10-04: an extra booked at 400 pays half its price, 200, inside the job's extras. At
    /// the re-grade's unbounded rates the job is 600 + 200 + 200 = 1 000, heavy adds 300, so the contract states
    /// 1 300; the company lowering its share to 0 after the take does not reprice the seat.
    /// </summary>
    [Fact]
    public async Task The_Extras_Booked_Are_Frozen_At_The_Take_And_Paid_As_The_Contract_States()
    {
        var order = ArrangeCompletedOrder(BookedHeavyRate, CleanerId);
        BookExtra(order, 400m);
        _rates = RatesAfterTheRegrade();
        var contractReward = await TakeAsync(order, CleanerId);

        _extrasSharePercent = "0";
        var pay = await PayAsync(CleanerId);

        Assert.Equal(1300m, contractReward);
        Assert.Equal((600m, 400m, 300m, 1300m), (pay.BasePay, pay.ExtrasPay, pay.DirtinessPay, pay.TotalPay));
    }

    /// <summary>
    /// At a 25 % share the extra booked at 400 pays 100: 600 + 200 + 100 = 900, heavy adds 270, 1 170.
    /// </summary>
    [Fact]
    public async Task A_Seat_With_No_Contract_Is_Paid_The_Extras_Booked_At_The_Share_In_Force()
    {
        var order = ArrangeCompletedOrder(BookedHeavyRate, CleanerId);
        BookExtra(order, 400m);
        _extrasSharePercent = "25";

        _rates = RatesAfterTheRegrade();
        var pay = await PayAsync(CleanerId);

        Assert.Equal((600m, 300m, 270m, 1170m), (pay.BasePay, pay.ExtrasPay, pay.DirtinessPay, pay.TotalPay));
    }

    private static void BookExtra(Order order, decimal price) =>
        order.AddSelectedExtras([OrderExtra.Create(order, Extra.Create("inside-oven", "Inside oven", null), price)]);

    [Fact]
    public async Task A_Seat_Paid_From_Its_Contract_Needs_No_Rate_Today()
    {
        var order = ArrangeCompletedOrder(BookedHeavyRate, CleanerId);
        _rates = RatesAtTheTake();
        await TakeAsync(order, CleanerId);
        _rates = [];

        var validation = await CreateValidator().ValidateAsync(new CalculateOrderPay.Command(OrderId, CleanerId));
        var pay = await PayAsync(CleanerId);

        Assert.True(validation.IsValid, string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
        Assert.Equal(1300m, pay.TotalPay);
    }

    [Fact]
    public async Task A_Seat_With_No_Contract_And_No_Rate_Is_Refused()
    {
        ArrangeCompletedOrder(BookedHeavyRate, CleanerId);

        var validation = await CreateValidator().ValidateAsync(new CalculateOrderPay.Command(OrderId, CleanerId));

        Assert.Equal(BusinessErrorMessage.NoPayConfiguration, Assert.Single(validation.Errors).ErrorMessage);
    }

    private CalculateOrderPay.Validator CreateValidator()
    {
        _orders.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _payPeriods.Setup(r => r.ExistsActivePeriodAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new CalculateOrderPay.Validator(
            _orders.Object,
            employees.Object,
            _payPeriods.Object,
            _payConfigs.Object,
            _pays.Object,
            Mock.Of<IReceivableRepository>(),
            Mock.Of<IRefundRepository>(),
            Mock.Of<ICreditAccountRepository>());
    }
}
