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
using Cleansia.Infra.Services.BusinessRegistry;
using FluentValidation.Results;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// Owner ruling 2026-10-04: a cleaner is approved only when the public register of the country they are
/// approved for holds their company ID (IČO), the business has not ended and a trade licence is in force.
/// A register that does not answer refuses the approval with its own answer, so the admin tries again.
/// No name is matched.
/// </summary>
public class ApproveEmployeeBusinessRegisterTests
{
    private const string EmployeeId = "emp-register";
    private const string CountryId = "country-cz-register";
    private const string Ico = "27082440";

    private static readonly string[] RegisterRefusals =
    [
        BusinessErrorMessage.RegistrationNumberNotRegistered,
        BusinessErrorMessage.EmployeeBusinessCeased,
        BusinessErrorMessage.EmployeeTradeLicenceInactive,
        BusinessErrorMessage.EmployeeBusinessRegistryUnavailable,
    ];

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICountryRepository> _countries = new();

    public ApproveEmployeeBusinessRegisterTests()
    {
        var employee = CompleteEmployee();
        _employees.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employees.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        var czechia = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        czechia.Id = CountryId;
        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(czechia);
    }

    public static TheoryData<BusinessRegistryRecord, string> Refusals => new()
    {
        { BusinessRegistryRecord.NotRegistered, BusinessErrorMessage.RegistrationNumberNotRegistered },
        { new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true), BusinessErrorMessage.EmployeeBusinessCeased },
        { new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false), BusinessErrorMessage.EmployeeTradeLicenceInactive },
        { BusinessRegistryRecord.Unavailable, BusinessErrorMessage.EmployeeBusinessRegistryUnavailable },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task The_Register_Refuses_The_Approval_On_The_Employee(BusinessRegistryRecord record, string refusal)
    {
        var result = await ValidateAsync(BusinessRegistryDoubles.Answering(record).Object);

        var failure = Assert.Single(result.Errors, e => RegisterRefusals.Contains(e.ErrorMessage));
        Assert.Equal(refusal, failure.ErrorMessage);
        Assert.Equal(nameof(ApproveEmployee.Command.EmployeeId), failure.PropertyName);
    }

    [Fact]
    public async Task A_Ceased_Business_Is_Reported_As_Ceased_Even_Without_A_Trade_Licence()
    {
        var result = await ValidateAsync(BusinessRegistryDoubles.Answering(
            new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: false)).Object);

        Assert.Equal(
            BusinessErrorMessage.EmployeeBusinessCeased,
            Assert.Single(result.Errors, e => RegisterRefusals.Contains(e.ErrorMessage)).ErrorMessage);
    }

    [Fact]
    public async Task A_Business_In_Force_With_A_Trade_Licence_Is_Approved_And_Was_Asked_In_The_Work_Country()
    {
        var registry = BusinessRegistryDoubles.Answering(BusinessRegistryDoubles.InForce());

        var result = await ValidateAsync(registry.Object);

        Assert.DoesNotContain(result.Errors, e => RegisterRefusals.Contains(e.ErrorMessage));
        registry.Verify(r => r.LookupAsync("CZE", Ico, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Register_That_Was_Not_Consulted_Does_Not_Refuse()
    {
        var result = await ValidateAsync(BusinessRegistryDoubles.NotConsulted());

        Assert.DoesNotContain(result.Errors, e => RegisterRefusals.Contains(e.ErrorMessage));
    }

    private async Task<ValidationResult> ValidateAsync(IBusinessRegistry registry)
    {
        var currencyResolution = new Mock<ICurrencyResolutionService>();
        currencyResolution
            .Setup(s => s.ResolveCurrencyForCountryAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Currency.Create("CZK", "Kč", "Czech koruna"));
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.GetAll()).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.GetAll()).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        var payConfigs = new Mock<IEmployeePayConfigRepository>();
        payConfigs.Setup(r => r.GetAll()).Returns(Array.Empty<EmployeePayConfig>().AsQueryable().BuildMock());
        var requirements = new Mock<IEmployeeDocumentRequirementRepository>();
        requirements
            .Setup(r => r.GetForCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var validator = new ApproveEmployee.Validator(
            _employees.Object, _countries.Object, services.Object, packages.Object,
            payConfigs.Object, requirements.Object, currencyResolution.Object,
            Mock.Of<IOperatorTenantResolver>(), Mock.Of<ITenantProvider>(),
            Mock.Of<ILegalDocumentResolver>(), Mock.Of<IUserConsentRepository>(),
            registry);

        return await validator.ValidateAsync(new ApproveEmployee.Command(EmployeeId, CountryId), CancellationToken.None);
    }

    private static Employee CompleteEmployee()
    {
        var user = User.CreateWithPassword("register@example.com", "Password1", "Jana", "Nováková");
        user.Id = "user-register";
        user.Update("Jana", "Nováková", "+420111222333", new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.UpdateEmployeeDetails(
            EmployeeEntityType.NaturalPerson,
            registrationNumber: Ico,
            legalEntityName: null,
            nationalityId: CountryId,
            passportId: "AB1234567",
            address: Address.Create("Main St 1", "Praha", "11000", CountryId),
            emergencyContactName: null,
            emergencyContactPhone: null);
        employee.UpdateBankDetails("CZ6508000000192000145399");
        return employee;
    }
}
