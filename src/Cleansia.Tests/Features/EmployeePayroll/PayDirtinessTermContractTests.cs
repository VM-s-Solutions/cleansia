using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using Moq;

namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// The dirtiness term a cleaner is paid sits outside the min/max clamp, so a pay row whose base,
/// extras and total are shown without it does not add up. The row and the period totals carry it.
/// </summary>
public class PayDirtinessTermContractTests
{
    private readonly Mock<IOrderEmployeePayRepository> _orderPayRepository = new();
    private readonly Mock<IPayPeriodRepository> _payPeriodRepository = new();
    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = new();

    public PayDirtinessTermContractTests()
    {
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString()));
        _payPeriodRepository
            .Setup(r => r.GetByIdAsync(PayrollMockFactory.PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayrollMockFactory.OpenPeriod());
        _invoiceRepository
            .Setup(r => r.GetAllForEmployeeAndPayPeriodAsync(
                PayrollMockFactory.EmployeeId, PayrollMockFactory.PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = PayrollMockFactory.CurrencyId;
        _currencyResolution
            .Setup(s => s.ResolveCurrencyForEmployeeAsync(PayrollMockFactory.EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(czk);
    }

    [Fact]
    public async Task Each_Row_States_Its_Dirtiness_Pay_And_The_Period_Totals_Sum_It()
    {
        _orderPayRepository
            .Setup(r => r.GetByEmployeeAndPeriodAsync(
                PayrollMockFactory.EmployeeId, PayrollMockFactory.PayPeriodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([PayWithDirtiness(basePay: 500m, dirtinessPay: 150m), PayWithDirtiness(basePay: 400m, dirtinessPay: 0m)]);

        var result = await new GetPeriodPays.Handler(
                Mock.Of<IEmployeeRepository>(),
                _payPeriodRepository.Object,
                _invoiceRepository.Object,
                _orderPayRepository.Object,
                Mock.Of<IOrderAccessService>(),
                _session.Object,
                _currencyResolution.Object,
                Mock.Of<ICurrencyRepository>())
            .Handle(new GetPeriodPays.Query(PayrollMockFactory.EmployeeId, PayrollMockFactory.PayPeriodId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var summary = result.Value!;
        Assert.Equal([150m, 0m], summary.OrderPays.Select(p => p.DirtinessPay));
        Assert.Equal(150m, summary.TotalDirtinessPay);
        Assert.Equal(1050m, summary.GrandTotal);
    }

    private static OrderEmployeePay PayWithDirtiness(decimal basePay, decimal dirtinessPay)
    {
        var pay = OrderEmployeePay.Create(
            orderId: $"order-{Guid.NewGuid():N}",
            employeeId: PayrollMockFactory.EmployeeId,
            payPeriodId: PayrollMockFactory.PayPeriodId,
            currencyId: PayrollMockFactory.CurrencyId,
            basePay: basePay,
            extrasPay: 0m,
            expensesPay: 0m,
            dirtinessPay: dirtinessPay,
            totalPay: basePay + dirtinessPay);
        pay.Id = $"oep-{Guid.NewGuid():N}";
        return pay;
    }
}
