using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// ADR-0061 D6 — a cleaner is employed by the operating company of the country they work in. The
/// employee row is loaded through the filter, so its tenant IS the approving admin's claim; the work
/// country's operator must be that claim, or the approval is refused. Keyed on the operator, not the
/// country: an admin of a company serving two countries approves a cleaner into either.
/// </summary>
public sealed class ApproveEmployeeWorkCountryOperatorTests
{
    private const string EmployeeId = "emp-1";
    private const string Czechia = "cze";
    private const string Slovakia = "svk";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<IOperatorTenantResolver> _operators = new();
    private readonly Mock<ITenantProvider> _tenant = new();

    public ApproveEmployeeWorkCountryOperatorTests()
    {
        var user = User.CreateWithPassword("cleaner@example.com", "Password1", "Clea", "Ner");
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        _employees.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employees.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        foreach (var country in new[] { Czechia, Slovakia })
        {
            _countries.Setup(r => r.ExistsAsync(country, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _countries.Setup(r => r.IsServicedAsync(country, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        _tenant.Setup(t => t.GetCurrentTenantId()).Returns("cleansia-cz");
    }

    private void OperatedBy(string countryId, string operatorTenantId) =>
        _operators.Setup(r => r.ResolveAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperatorResolution(true, operatorTenantId));

    private ApproveEmployee.Validator Validator()
    {
        var requirements = new Mock<IEmployeeDocumentRequirementRepository>();
        requirements.Setup(r => r.GetForCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var currencyResolution = new Mock<ICurrencyResolutionService>();
        currencyResolution
            .Setup(s => s.ResolveCurrencyForCountryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Currency.Create("CZK", "Kč", "Czech koruna"));
        // An empty catalogue: the pay-coverage gate has nothing to judge, so the cases stay about the
        // operator rule. ApproveEmployeePayCoverageTests owns the gate.
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.GetAll()).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.GetAll()).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        var payConfigs = new Mock<IEmployeePayConfigRepository>();
        payConfigs.Setup(r => r.GetAll()).Returns(Array.Empty<EmployeePayConfig>().AsQueryable().BuildMock());
        return new ApproveEmployee.Validator(
            _employees.Object,
            _countries.Object,
            services.Object,
            packages.Object,
            payConfigs.Object,
            requirements.Object,
            currencyResolution.Object,
            _operators.Object,
            _tenant.Object);
    }

    [Fact]
    public async Task A_Work_Country_Operated_By_Another_Company_Is_Refused_On_The_Work_Country()
    {
        OperatedBy(Slovakia, "cleansia-sk");

        var result = await Validator().ValidateAsync(new ApproveEmployee.Command(EmployeeId, Slovakia));

        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeWorkCountryOperatorMismatch);
        Assert.Equal(nameof(ApproveEmployee.Command.WorkCountryId), failure.PropertyName);
    }

    [Fact]
    public async Task A_Work_Country_Operated_By_The_Admins_Own_Company_Passes_The_Rule()
    {
        OperatedBy(Slovakia, "cleansia-cz");

        var result = await Validator().ValidateAsync(new ApproveEmployee.Command(EmployeeId, Slovakia));

        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeWorkCountryOperatorMismatch);
    }

    [Fact]
    public async Task The_Rule_Runs_After_The_Serviced_Check_So_An_Unserviced_Country_Keeps_Its_Own_Key()
    {
        _countries.Setup(r => r.IsServicedAsync(Slovakia, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Validator().ValidateAsync(new ApproveEmployee.Command(EmployeeId, Slovakia));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CountryNotServiced);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeWorkCountryOperatorMismatch);
        _operators.Verify(r => r.ResolveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
