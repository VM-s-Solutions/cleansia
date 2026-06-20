using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// Characterization of UpdateEmployee.Validator focused on the rules that the B3 base-class
/// composition refactor moves — the first-name / last-name / email rules previously supplied by the
/// BaseUserValidator helper methods — plus the ownership and existence rules that stay put. Pins the
/// emitted BusinessErrorMessage codes so the refactor (AbstractValidator + composed shared rules)
/// is behavior-preserving.
/// </summary>
public class UpdateEmployeeValidatorTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cz";

    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ITaxIdValidator> _taxIdValidator = new();

    private UpdateEmployee.Validator CreateValidator() => new(
        _countryRepository.Object,
        _employeeRepository.Object,
        _session.Object,
        _taxIdValidator.Object);

    private void ArrangePassingContext()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _employeeRepository
            .Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _countryRepository.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _taxIdValidator
            .Setup(v => v.ValidateRegistrationNumberAsync(It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
        _taxIdValidator
            .Setup(v => v.ValidateVatNumberAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
    }

    private static UpdateEmployee.Command Valid() => new(
        EmployeeId: EmployeeId,
        FirstName: "First",
        LastName: "Last",
        BirthDate: new DateOnly(1990, 1, 1),
        Street: "Main Street 10",
        City: "Prague",
        ZipCode: "11000",
        CountryId: CountryId,
        State: null,
        NationalityId: CountryId,
        Phone: "+420123456789",
        Email: "cleaner@cleansia.cz",
        PassportId: "AB12345",
        EntityType: EmployeeEntityType.NaturalPerson,
        RegistrationNumber: "12345678",
        VatNumber: null,
        LegalEntityName: null,
        Iban: "CZ6508000000192000145399",
        EmergencyName: null,
        EmergencyPhone: null,
        Consent: true);

    [Fact]
    public async Task Valid_Command_Passes()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Not_Owner_Fails_NotAllowedToUpdateEmployee()
    {
        ArrangePassingContext();
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotAllowedToUpdateEmployee);
    }

    [Fact]
    public async Task Unknown_Employee_Fails_NotFound()
    {
        ArrangePassingContext();
        _employeeRepository.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.EmployeeId)
            && e.ErrorMessage == BusinessErrorMessage.NotFound);
    }

    [Fact]
    public async Task Empty_FirstName_Fails_Required()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with { FirstName = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.FirstName)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task FirstName_Too_Long_Fails_MaxLength()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with { FirstName = new string('x', 51) });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.FirstName)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }

    [Fact]
    public async Task Empty_LastName_Fails_Required()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with { LastName = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.LastName)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Empty_Email_Fails_Required()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with { Email = string.Empty });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.Email)
            && e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Invalid_Email_Fails_InvalidEmailFormat()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with { Email = "not-an-email" });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.Email)
            && e.ErrorMessage == BusinessErrorMessage.InvalidEmailFormat);
    }

    [Fact]
    public async Task Email_Too_Long_Fails_MaxLength()
    {
        ArrangePassingContext();

        var longLocal = new string('a', 45);
        var result = await CreateValidator().ValidateAsync(Valid() with { Email = $"{longLocal}@cleansia.cz" });

        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UpdateEmployee.Command.Email)
            && e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }
}
