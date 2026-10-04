using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Services.BusinessRegistry;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// Characterization of UpdateEmployee.Validator focused on the rules that the B3 base-class
/// composition refactor moves — the first-name / last-name rules previously supplied by the
/// BaseUserValidator helper methods — plus the ownership and existence rules that stay put. Pins the
/// emitted BusinessErrorMessage codes so the refactor (AbstractValidator + composed shared rules)
/// is behavior-preserving.
/// </summary>
public class UpdateEmployeeValidatorTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cz";
    private const string SlovakiaId = "sk";

    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ITaxIdValidator> _taxIdValidator = new();
    private Mock<IBusinessRegistry> _registry = BusinessRegistryDoubles.Answering(BusinessRegistryDoubles.InForce());
    private Employee _employee = null!;

    private UpdateEmployee.Validator CreateValidator() => new(
        _countryRepository.Object,
        _employeeRepository.Object,
        _session.Object,
        _taxIdValidator.Object,
        _registry.Object);

    private void ArrangePassingContext()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        _employee = employee;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _employeeRepository
            .Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _countryRepository.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Czechia", "CZE", "CZ", isServiced: true));
        _countryRepository.Setup(r => r.ExistsAsync(SlovakiaId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository.Setup(r => r.IsServicedAsync(SlovakiaId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository
            .Setup(r => r.GetByIdAsync(SlovakiaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Slovakia", "SVK", "SK", isServiced: true));
        _taxIdValidator
            .Setup(v => v.ValidateRegistrationNumberAsync(It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
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
        PassportId: "AB12345",
        EntityType: EmployeeEntityType.NaturalPerson,
        RegistrationNumber: "12345678",
        LegalEntityName: null,
        EmergencyName: null,
        EmergencyPhone: null,
        Consent: true);

    /// <summary>
    /// Owner ruling 2026-10-04: the cleaner's own save asks the register of the country the number is
    /// judged against whether it exists, and nothing more.
    /// </summary>
    [Fact]
    public async Task A_Number_The_Business_Register_Does_Not_Hold_Is_Refused_On_The_Registration_Number()
    {
        ArrangePassingContext();
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotRegistered);

        var result = await CreateValidator().ValidateAsync(Valid());

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.RegistrationNumberNotRegistered, failure.ErrorMessage);
        Assert.Equal(nameof(UpdateEmployee.Command.RegistrationNumber), failure.PropertyName);
        _registry.Verify(r => r.LookupAsync("CZE", "12345678", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// ARES answers only for Czechia, so an address abroad asked no register and an approved cleaner could
    /// swap in any number with it. Once approved, the register is the work country's, the one approval asked.
    /// </summary>
    [Fact]
    public async Task An_Approved_Cleaner_With_An_Address_Abroad_Is_Still_Checked_In_The_Work_Countrys_Register()
    {
        ArrangePassingContext();
        _employee.AssignWorkCountry(CountryId);
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotConsulted);
        _registry
            .Setup(r => r.LookupAsync("CZE", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessRegistryRecord.NotRegistered);

        var result = await CreateValidator().ValidateAsync(Valid() with { CountryId = SlovakiaId });

        Assert.Equal(BusinessErrorMessage.RegistrationNumberNotRegistered, Assert.Single(result.Errors).ErrorMessage);
        _registry.Verify(r => r.LookupAsync("CZE", "12345678", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Register_That_Does_Not_Answer_Lets_The_Save_Through()
    {
        ArrangePassingContext();
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.Unavailable);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Number_In_The_Wrong_Format_Is_Not_Taken_To_The_Register()
    {
        ArrangePassingContext();
        _taxIdValidator
            .Setup(v => v.ValidateRegistrationNumberAsync(It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Invalid(BusinessErrorMessage.RegistrationNumberInvalidFormat));

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Equal(BusinessErrorMessage.RegistrationNumberInvalidFormat, Assert.Single(result.Errors).ErrorMessage);
        _registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Valid_Command_Passes()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Caller_With_No_Employee_Record_Fails_NotAllowedToUpdateEmployee()
    {
        ArrangePassingContext();
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Employee?)null);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotAllowedToUpdateEmployee);
    }

    /// <summary>
    /// The id on the wire is inert (S1, [OWN-DATA]): the subject is the JWT caller, so neither an id
    /// naming another cleaner nor no id at all is a validation concern. The rule this replaces made the
    /// route uncallable for any client that cannot supply the id, while stopping no attacker.
    /// </summary>
    [Theory]
    [InlineData("emp-somebody-else")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Any_Employee_Id_Passes_Because_The_Id_Is_Never_Read(string? employeeId)
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with { EmployeeId = employeeId });

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// A cleaner contracts with the platform as a natural person (owner ruling 2026-09-20, on the
    /// lawyer's advice). The enum member and the columns stay for the rows that already carry it and
    /// for the administrator, who may still onboard a company by hand; the cleaner's own writes refuse
    /// it with one reason, and nothing else — a second "legal entity name is required" would send the
    /// cleaner to fill in a field for a choice that is not on offer.
    /// </summary>
    [Fact]
    public async Task A_Change_To_A_Legal_Entity_Is_Refused_With_LegalEntityNotAccepted_And_Nothing_Else()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with
        {
            EntityType = EmployeeEntityType.LegalEntity,
            LegalEntityName = null,
        });

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(UpdateEmployee.Command.EntityType), error.PropertyName);
        Assert.Equal(BusinessErrorMessage.LegalEntityNotAccepted, error.ErrorMessage);
    }

    /// <summary>
    /// The refusal is of a CHANGE to a company, not of the word: a row an operator already set to a
    /// company is not changing anything by sending it back, and refusing it would leave that cleaner
    /// unable to save at all — or, sending the only value on offer, silently demoted (the handler keeps
    /// the stored pair either way).
    /// </summary>
    [Theory]
    [InlineData(EmployeeEntityType.LegalEntity)]
    [InlineData(EmployeeEntityType.NaturalPerson)]
    public async Task A_Row_Already_A_Legal_Entity_Passes_Whatever_Type_The_Command_Carries(EmployeeEntityType sent)
    {
        ArrangePassingContext();
        var employee = await _employeeRepository.Object.GetByUserEmailAsync(UserEmail);
        employee!.UpdateBusinessIdentity(EmployeeEntityType.LegalEntity, "12345678", "Uklid s.r.o.");

        var result = await CreateValidator().ValidateAsync(Valid() with { EntityType = sent, LegalEntityName = null });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Natural_Person_Passes_Whatever_LegalEntityName_Carries()
    {
        ArrangePassingContext();

        var result = await CreateValidator().ValidateAsync(Valid() with
        {
            EntityType = EmployeeEntityType.NaturalPerson,
            LegalEntityName = new string('x', 201),
        });

        Assert.True(result.IsValid);
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
}
