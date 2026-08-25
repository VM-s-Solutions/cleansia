using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The approval half of the pay gate: a cleaner may not reach <c>ContractStatus.Approved</c> while any
/// active catalogue entry has no pay config that applies to them, so nobody steps into the partner app
/// and finds a board of blank pay.
///
/// <para>Two properties matter beyond "it refuses". <b>The fallback still works</b> — a cleaner with no
/// personal configs at all is approvable against a full platform-wide set, because that is what the
/// estimator resolves. And <b>the refusal names the entry</b>: an admin who reads only "cannot approve"
/// has no way to act and will route around the gate, so each uncovered entry contributes its own
/// failure carrying its name.</para>
/// </summary>
public class ApproveEmployeePayCoverageTests
{
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cze";
    private const string CurrencyId = "czk";
    private const string ServiceId = "svc-general";
    private const string ServiceName = "General Cleaning";
    private const string PackageId = "pkg-essential";
    private const string PackageName = "Essential Clean";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IPackageRepository> _packages = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigs = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IAuditContext> _audit = new();

    private readonly Employee _employee;

    public ApproveEmployeePayCoverageTests()
    {
        _employee = CompleteEmployee();

        _employees.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        _employees.Setup(r => r.GetQueryable()).Returns(new[] { _employee }.AsQueryable().BuildMock());

        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var admin = User.CreateWithPassword("admin@cleansia.cz", "Password1", "Ad", "Min");
        admin.Id = "admin-1";
        _session.Setup(s => s.GetUserEmail()).Returns("admin@cleansia.cz");
        _users.Setup(r => r.GetByEmailAsync("admin@cleansia.cz", It.IsAny<CancellationToken>())).ReturnsAsync(admin);

        ArrangeCatalogue(ActiveService(), ActivePackage());
        ArrangeConfigs();
    }

    private static Employee CompleteEmployee()
    {
        var user = User.CreateWithPassword("cleaner@example.com", "Password1", "Clea", "Ner");
        user.Id = "user-1";
        user.Update("Clea", "Ner", "+420111222333", new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.UpdateEmployeeDetails(
            EmployeeEntityType.NaturalPerson,
            registrationNumber: "12345678",
            vatNumber: null,
            legalEntityName: null,
            nationalityId: CountryId,
            passportId: "AB1234567",
            address: Address.Create("Main St 1", "Praha", "11000", CountryId),
            availability: new Dictionary<string, List<TimeRange>>(),
            emergencyContactName: null,
            emergencyContactPhone: null);
        employee.UpdateBankDetails("CZ6508000000192000145399");

        return employee;
    }

    private static Service ActiveService(bool isActive = true)
    {
        var service = Service.Create(
            categoryId: "cat-1", name: ServiceName, description: "d", basePrice: 500m, perRoomPrice: 150m);
        service.Id = ServiceId;
        service.IsActive = isActive;
        return service;
    }

    private static Package ActivePackage(bool isActive = true)
    {
        var package = Package.Create(PackageName, "d", 799m);
        package.Id = PackageId;
        package.IsActive = isActive;
        return package;
    }

    private void ArrangeCatalogue(Service? service, Package? package)
    {
        _services.Setup(r => r.GetAll())
            .Returns((service is null ? Array.Empty<Service>() : [service]).AsQueryable().BuildMock());
        _packages.Setup(r => r.GetAll())
            .Returns((package is null ? Array.Empty<Package>() : [package]).AsQueryable().BuildMock());
    }

    private void ArrangeConfigs(params EmployeePayConfig[] configs) =>
        _payConfigs.Setup(r => r.GetAll()).Returns(configs.AsQueryable().BuildMock());

    private static EmployeePayConfig ServiceConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForService(ServiceId, 250m, CurrencyId, employeeId: employeeId);

    private static EmployeePayConfig PackageConfig(string? employeeId = null) =>
        EmployeePayConfig.CreateForPackage(PackageId, 400m, CurrencyId, employeeId: employeeId);

    // No document requirements configured for the country, so the documents gate passes and these
    // cases stay about PAY coverage. ApproveEmployeeDocumentGateTests owns the gate itself.
    private readonly Mock<IEmployeeDocumentRequirementRepository> _documentRequirements = new();

    private ApproveEmployee.Validator CreateValidator()
    {
        _documentRequirements
            .Setup(r => r.GetForCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new(
            _employees.Object, _countries.Object, _services.Object, _packages.Object,
            _payConfigs.Object, _documentRequirements.Object);
    }

    private ApproveEmployee.Handler CreateHandler() => new(
        _employees.Object,
        _users.Object,
        _session.Object,
        _audit.Object,
        _services.Object,
        _packages.Object,
        _payConfigs.Object);

    private static ApproveEmployee.Command Command() => new(EmployeeId, CountryId, Notes: null);

    [Fact]
    public async Task Approval_Is_Refused_While_An_Active_Service_Has_No_Pay_Config()
    {
        ArrangeConfigs(PackageConfig());

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeePayConfigMissing);
    }

    [Fact]
    public async Task The_Refusal_Names_The_Uncovered_Entry()
    {
        ArrangeConfigs(PackageConfig());

        var result = await CreateValidator().ValidateAsync(Command());

        var failure = Assert.Single(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeePayConfigMissing);
        Assert.Equal(ServiceName, failure.ErrorCode);
    }

    [Fact]
    public async Task Every_Uncovered_Entry_Is_Named_Not_Just_The_First()
    {
        ArrangeConfigs();

        var result = await CreateValidator().ValidateAsync(Command());

        var named = result.Errors
            .Where(e => e.ErrorMessage == BusinessErrorMessage.EmployeePayConfigMissing)
            .Select(e => e.ErrorCode)
            .ToList();

        Assert.Equal(2, named.Count);
        Assert.Contains(ServiceName, named);
        Assert.Contains(PackageName, named);
    }

    [Fact]
    public async Task Approval_Is_Allowed_Once_The_Platform_Wide_Configs_Exist()
    {
        ArrangeConfigs(ServiceConfig(), PackageConfig());

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The fallback the defect report turned on: a cleaner with NO personal configs is quotable off the
    /// platform-wide rows, so the gate must not demand per-employee ones.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_With_No_Personal_Configs_At_All_Is_Approvable()
    {
        ArrangeConfigs(ServiceConfig(employeeId: null), PackageConfig(employeeId: null));

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeePayConfigMissing);
    }

    [Fact]
    public async Task A_Personal_Override_Covers_An_Entry_With_No_Platform_Wide_Row()
    {
        ArrangeConfigs(ServiceConfig(employeeId: EmployeeId), PackageConfig(employeeId: EmployeeId));

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Another_Cleaners_Override_Does_Not_Cover_This_One()
    {
        ArrangeConfigs(ServiceConfig(employeeId: "emp-2"), PackageConfig(employeeId: "emp-2"));

        var result = await CreateValidator().ValidateAsync(Command());

        var named = result.Errors
            .Where(e => e.ErrorMessage == BusinessErrorMessage.EmployeePayConfigMissing)
            .Select(e => e.ErrorCode)
            .ToList();

        Assert.Equal(2, named.Count);
    }

    /// <summary>A soft-deleted entry is out of the catalogue and must not stand between a cleaner and approval.</summary>
    [Fact]
    public async Task A_Deactivated_Entry_With_No_Config_Does_Not_Block_Approval()
    {
        ArrangeCatalogue(ActiveService(isActive: false), ActivePackage(isActive: false));
        ArrangeConfigs();

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task The_Handler_Refuses_Too_So_The_Gate_Is_Not_Validator_Only()
    {
        ArrangeConfigs();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.EmployeePayConfigMissing, result.Error!.Message);
        Assert.NotEqual(ContractStatus.Approved, _employee.ContractStatus);
    }

    [Fact]
    public async Task The_Handler_Approves_When_The_Catalogue_Is_Covered()
    {
        ArrangeConfigs(ServiceConfig(), PackageConfig());

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ContractStatus.Approved, _employee.ContractStatus);
    }

    /// <summary>
    /// Anti-vacuity: the fixture really is approvable for every reason OTHER than pay, so a green
    /// "allowed" case is not being produced by a profile rule that happens to pass on an empty employee.
    /// </summary>
    [Fact]
    public async Task The_Fixture_Is_Otherwise_Approvable()
    {
        ArrangeConfigs(ServiceConfig(), PackageConfig());

        var result = await CreateValidator().ValidateAsync(Command());

        Assert.True(_employee.IsProfileComplete());
        Assert.Empty(result.Errors);
    }
}
