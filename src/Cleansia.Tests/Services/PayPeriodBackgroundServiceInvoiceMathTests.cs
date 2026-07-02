using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.Pdf;
using Cleansia.TestUtilities.MockDataFactories.Currencies;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The auto-close job's invoice money math (<see cref="PayPeriodBackgroundService"/>): the invoice
/// it creates when a period closes must bill the persisted per-order <c>TotalPay</c> — the min/max
/// clamped amount the period-pays preview (GrandTotal = Σ TotalPay) shows — never a re-sum of the
/// raw pay components, and totals must be byte-identical to before when no clamp fired. The run is
/// arranged to stop before PDF generation (no language row); the invoice is added before that point.
/// </summary>
public class PayPeriodBackgroundServiceInvoiceMathTests
{
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<ICompanyInfoRepository> _companyInfoRepository = new();
    private readonly Mock<ILanguageRepository> _languageRepository = new();
    private readonly Mock<ICountryInvoiceConfigRepository> _countryInvoiceConfigRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly Mock<IPdfService> _pdfService = new();
    private readonly Mock<IBlobContainerClientFactory> _blobContainerClientFactory = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private EmployeeInvoice? _addedInvoice;

    private PayPeriodBackgroundService CreateService() => new(
        _payPeriodRepository.Object,
        _employeeRepository.Object,
        _emailService.Object,
        _unitOfWork.Object,
        NullLogger<PayPeriodBackgroundService>.Instance,
        _currencyRepository.Object,
        _invoiceRepository.Object,
        _orderPayRepository.Object,
        _companyInfoRepository.Object,
        _languageRepository.Object,
        _countryInvoiceConfigRepository.Object,
        _countryConfigurationRepository.Object,
        _pdfService.Object,
        _blobContainerClientFactory.Object,
        _tenantProvider.Object);

    private void ArrangePeriodCloseWithPays(params OrderEmployeePay[] pays)
    {
        var expiredPeriod = PayrollMockFactory.OpenPeriod();
        _payPeriodRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(new[] { expiredPeriod }.AsQueryable().BuildMock());
        _payPeriodRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { PayrollMockFactory.OpenPeriod() }.AsQueryable().BuildMock());

        var employee = Employee.CreateWithUser(
            User.CreateWithPassword("emp@cleansia.test", "Password1", "First", "Last"));
        employee.Id = PayrollMockFactory.EmployeeId;
        _employeeRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { employee }.AsQueryable().BuildMock());

        _invoiceRepository
            .Setup(r => r.GetQueryable())
            .Returns(Array.Empty<EmployeeInvoice>().AsQueryable().BuildMock());
        _invoiceRepository
            .Setup(r => r.Add(It.IsAny<EmployeeInvoice>()))
            .Callback<EmployeeInvoice>(i => _addedInvoice = i);

        _orderPayRepository
            .Setup(r => r.GetUnassignedForEmployeePeriodAsync(
                employee.Id, expiredPeriod.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pays);

        var currency = CurrencyMockFactory.Generate();
        currency.Id = PayrollMockFactory.CurrencyId;
        _currencyRepository
            .Setup(r => r.GetDefaultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
    }

    [Fact]
    public async Task Clamped_Pay_Is_Invoiced_At_Persisted_TotalPay_Not_Raw_Components()
    {
        // MaximumPay clamped the first order to 400 at calculation time (its components sum to 650).
        var pays = new[]
        {
            ClampedOrderPay(basePay: 500m, extrasPay: 100m, expensesPay: 50m, clampedTotalPay: 400m),
            PayrollMockFactory.OrderPay(basePay: 100m)
        };
        ArrangePeriodCloseWithPays(pays);

        await CreateService().CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);

        Assert.NotNull(_addedInvoice);
        Assert.Equal(pays.Sum(p => p.TotalPay), _addedInvoice!.TotalAmount);
        Assert.Equal(500m, _addedInvoice.SubTotal);
        Assert.Equal(500m, _addedInvoice.TotalAmount);
    }

    [Fact]
    public async Task Unclamped_Pays_Keep_The_Same_Invoice_Totals()
    {
        var pays = new[]
        {
            PayrollMockFactory.OrderPay(basePay: 100.50m, extrasPay: 10m, expensesPay: 5.25m, bonusPay: 20m, deductionPay: 3m),
            PayrollMockFactory.OrderPay(basePay: 200m, extrasPay: 0m, expensesPay: 12.75m, bonusPay: 0m, deductionPay: 8m)
        };
        ArrangePeriodCloseWithPays(pays);

        await CreateService().CloseExpiredPeriodsAndOpenNewAsync(CancellationToken.None);

        Assert.NotNull(_addedInvoice);
        Assert.Equal(328.50m, _addedInvoice!.SubTotal);
        Assert.Equal(20m, _addedInvoice.BonusAmount);
        Assert.Equal(11m, _addedInvoice.DeductionAmount);
        Assert.Equal(337.50m, _addedInvoice.TotalAmount);
    }

    // PayrollMockFactory derives TotalPay from the components, so a pay where the min/max clamp
    // fired (TotalPay < components sum) has to be created directly.
    private static OrderEmployeePay ClampedOrderPay(
        decimal basePay, decimal extrasPay, decimal expensesPay, decimal clampedTotalPay) =>
        OrderEmployeePay.Create(
            orderId: $"order-{Guid.NewGuid():N}",
            employeeId: PayrollMockFactory.EmployeeId,
            payPeriodId: PayrollMockFactory.PayPeriodId,
            basePay: basePay,
            extrasPay: extrasPay,
            expensesPay: expensesPay,
            totalPay: clampedTotalPay);
}
