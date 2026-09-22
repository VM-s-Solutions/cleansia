using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The partial identification update the mobile partner hosts route. It carries the same entity-type
/// choice as the full profile save, so it owes the same refusal: a cleaner contracts with the platform
/// as a natural person, and a company is onboarded by an administrator or not at all.
/// </summary>
public class UpdateIdentificationInfoValidatorTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cz";

    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ITaxIdValidator> _taxIdValidator = new();

    private UpdateIdentificationInfo.Validator CreateValidator() => new(
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
        _countryRepository.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _taxIdValidator
            .Setup(v => v.ValidateRegistrationNumberAsync(It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
    }

    private static UpdateIdentificationInfo.Command Valid() => new(
        EmployeeId: EmployeeId,
        NationalityId: CountryId,
        PassportId: "AB12345",
        EntityType: EmployeeEntityType.NaturalPerson,
        BusinessCountryId: CountryId,
        RegistrationNumber: "12345678",
        LegalEntityName: null);

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
        Assert.Equal(nameof(UpdateIdentificationInfo.Command.EntityType), error.PropertyName);
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
}
