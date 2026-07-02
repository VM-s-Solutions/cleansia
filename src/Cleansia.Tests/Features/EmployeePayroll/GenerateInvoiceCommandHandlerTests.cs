using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The <see cref="GenerateInvoice.Handler"/> money aggregation: the created invoice sums the
/// fetched order pays exactly, every fetched pay is assigned to the new invoice so a second
/// generation can't double-bill, and a net-negative pay set clamps the total to zero.
/// Mocked repositories; no DB.
/// </summary>
public class GenerateInvoiceCommandHandlerTests
{
    private const string EmployeeId = PayrollMockFactory.EmployeeId;
    private const string PayPeriodId = PayrollMockFactory.PayPeriodId;

    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();

    private readonly Currency _currency = CurrencyMockFactory.Generate();

    public GenerateInvoiceCommandHandlerTests()
    {
        _currency.Id = PayrollMockFactory.CurrencyId;
        _currencyResolution
            .Setup(s => s.ResolveCurrencyCodeForEmployeeAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_currency);
    }

    private GenerateInvoice.Handler CreateHandler() => new(
        _currencyRepository.Object,
        _currencyResolution.Object,
        _invoiceRepository.Object,
        _orderPayRepository.Object);

    private void ArrangeOrderPays(params OrderEmployeePay[] pays) =>
        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(EmployeeId, PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);

    [Fact]
    public async Task Sums_Order_Pays_Into_Invoice_Exactly()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100.50m, extrasPay: 10m, expensesPay: 5.25m, bonusPay: 20m, deductionPay: 3m),
            PayrollMockFactory.OrderPay(basePay: 200m, extrasPay: 0m, expensesPay: 12.75m, bonusPay: 0m, deductionPay: 8m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(328.50m, added!.SubTotal); // (100.50+10+5.25) + (200+0+12.75)
        Assert.Equal(20m, added.BonusAmount);
        Assert.Equal(11m, added.DeductionAmount);
        Assert.Equal(2, added.TotalOrders);
        Assert.Equal(337.50m, added.TotalAmount); // 328.50 + 20 - 11
        _invoiceRepository.Verify(r => r.Add(It.IsAny<EmployeeInvoice>()), Times.Once);
    }

    [Fact]
    public async Task Assigns_Every_Fetched_Pay_To_The_New_Invoice()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 50m),
            PayrollMockFactory.OrderPay(basePay: 75m),
            PayrollMockFactory.OrderPay(basePay: 25m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(pays, p => Assert.Equal(added!.Id, p.EmployeeInvoiceId));
    }

    [Fact]
    public async Task Clamped_Pay_Is_Invoiced_At_Persisted_TotalPay_Not_Raw_Components()
    {
        // MaximumPay clamped the first order to 400 at calculation time (its components sum to
        // 650). The invoice must bill the persisted TotalPay — the same basis the period-pays
        // preview (GrandTotal = Σ TotalPay) shows the admin and the employee.
        var pays = new[]
        {
            ClampedOrderPay(basePay: 500m, extrasPay: 100m, expensesPay: 50m, clampedTotalPay: 400m),
            PayrollMockFactory.OrderPay(basePay: 100m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(pays.Sum(p => p.TotalPay), added!.TotalAmount);
        Assert.Equal(500m, added.SubTotal);
        Assert.Equal(500m, added.TotalAmount);
    }

    [Fact]
    public async Task Net_Negative_Pay_Set_Clamps_Invoice_Total_To_Zero()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100m, bonusPay: 0m, deductionPay: 500m)
        };
        ArrangeOrderPays(pays);
        EmployeeInvoice? added = null;
        _invoiceRepository.Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => added = i);

        var result = await CreateHandler().Handle(
            new GenerateInvoice.Command(EmployeeId, PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, added!.TotalAmount);
    }

    // PayrollMockFactory derives TotalPay from the components, so a pay where the min/max clamp
    // fired (TotalPay < components sum) has to be created directly.
    private static OrderEmployeePay ClampedOrderPay(
        decimal basePay, decimal extrasPay, decimal expensesPay, decimal clampedTotalPay) =>
        OrderEmployeePay.Create(
            orderId: $"order-{Guid.NewGuid():N}",
            employeeId: EmployeeId,
            payPeriodId: PayPeriodId,
            basePay: basePay,
            extrasPay: extrasPay,
            expensesPay: expensesPay,
            totalPay: clampedTotalPay);
}
