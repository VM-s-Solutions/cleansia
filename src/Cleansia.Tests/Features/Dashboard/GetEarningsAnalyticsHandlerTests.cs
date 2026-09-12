using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.EmployeePayroll;
using MediatR;
using Moq;

namespace Cleansia.Tests.Features.Dashboard;

/// <summary>
/// The earnings chart is labelled with the dashboard's currency code, so every figure on it is a sum
/// over the invoices in that currency only. An invoice in another currency is not on this chart;
/// adding it in would put a EUR amount under a Kč label (T-0702).
/// </summary>
public class GetEarningsAnalyticsHandlerTests
{
    private const string CallerEmployeeId = "emp-1";

    private readonly Mock<IEmployeeInvoiceRepository> _invoiceRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolution = new();

    public GetEarningsAnalyticsHandlerTests()
    {
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));
        _orderAccessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CallerEmployeeId);

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = PayrollMockFactory.CurrencyId;
        _currencyResolution
            .Setup(s => s.ResolveCurrencyForEmployeeAsync(CallerEmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(czk);
    }

    [Fact]
    public async Task Totals_And_Months_Cover_Only_The_Employees_Currency()
    {
        _invoiceRepository
            .Setup(r => r.GetByEmployeeAndDateRangeAsync(
                CallerEmployeeId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                PayrollMockFactory.Invoice(subTotal: 500m, generatedAt: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc)),
                PayrollMockFactory.Invoice(subTotal: 700m, generatedAt: new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc)),
                PayrollMockFactory.Invoice(subTotal: 20m, currencyId: "currency-eur", generatedAt: new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc)),
            ]);

        var result = await CreateHandler().Handle(
            new GetEarningsAnalytics.Query(null, new DateTime(2026, 1, 1), new DateTime(2026, 3, 1)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value!;
        Assert.Equal(1200m, dto.TotalEarnings);
        Assert.Equal(2, dto.MonthlyEarnings.Count());
        Assert.Equal(700m, dto.MonthlyEarnings.Single(m => m.Month == 2).Amount);
        Assert.Equal(700m, dto.HighestMonth!.Amount);
        Assert.Equal(1200m, dto.Breakdown.TotalAmount);
    }

    // The handler is internal; build it by reflection, as CompanyVatLeverTests does.
    private IRequestHandler<GetEarningsAnalytics.Query, BusinessResult<EarningsAnalyticsDto>> CreateHandler() =>
        (IRequestHandler<GetEarningsAnalytics.Query, BusinessResult<EarningsAnalyticsDto>>)Activator.CreateInstance(
            typeof(GetEarningsAnalytics).GetNestedType("Handler", BindingFlags.NonPublic)!,
            _invoiceRepository.Object,
            _orderAccessService.Object,
            _session.Object,
            _currencyResolution.Object)!;
}
