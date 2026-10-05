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
/// The partial identification update the mobile partner hosts route. It carries the same entity-type
/// choice as the full profile save, so it owes the same refusal: a cleaner contracts with the platform
/// as a natural person, and a company is onboarded by an administrator or not at all.
/// </summary>
public class UpdateIdentificationInfoValidatorTests
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

    private UpdateIdentificationInfo.Validator CreateValidator() => new(
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
        employee.UpdateAddress(Address.Create("Main Street 10", "Prague", "11000", CountryId));
        _employee = employee;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _countryRepository.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository
            .Setup(r => r.GetByIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Czechia", "CZE", "CZ", isServiced: true));
        _countryRepository.Setup(r => r.ExistsAsync(SlovakiaId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository
            .Setup(r => r.GetByIdAsync(SlovakiaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Slovakia", "SVK", "SK", isServiced: true));
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

    /// <summary>
    /// Owner ruling 2026-10-04: the cleaner's own save asks the register of the business country whether
    /// the number exists. Only that refuses: an ended business or one without a trade licence is the
    /// approval's to judge, and a register that does not answer never blocks the save.
    /// </summary>
    [Fact]
    public async Task A_Number_The_Business_Register_Does_Not_Hold_Is_Refused_On_The_Registration_Number()
    {
        ArrangePassingContext();
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotRegistered);

        var result = await CreateValidator().ValidateAsync(Valid());

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.RegistrationNumberNotRegistered, failure.ErrorMessage);
        Assert.Equal(nameof(UpdateIdentificationInfo.Command.RegistrationNumber), failure.PropertyName);
        _registry.Verify(r => r.LookupAsync("CZE", "12345678", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The business country is the app's to send and no column keeps it, and ARES answers only for Czechia.
    /// Naming Slovakia asked no register, so an approved cleaner could swap in any number. The register is
    /// the one approval asked: the work country's, whatever the address or the app says.
    /// </summary>
    [Fact]
    public async Task An_Approved_Cleaner_Naming_Another_Country_Is_Still_Checked_In_The_Work_Countrys_Register()
    {
        ArrangePassingContext();
        _employee.AssignWorkCountry(CountryId);
        _employee.UpdateAddress(Address.Create("Hlavna 1", "Bratislava", "81101", SlovakiaId));
        _registry = AresOnly(BusinessRegistryRecord.NotRegistered);

        var result = await CreateValidator().ValidateAsync(Valid() with { BusinessCountryId = SlovakiaId });

        Assert.Equal(BusinessErrorMessage.RegistrationNumberNotRegistered, Assert.Single(result.Errors).ErrorMessage);
        _registry.Verify(r => r.LookupAsync("CZE", "12345678", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Cleaner_Not_Yet_Approved_Is_Checked_In_The_Register_Of_Their_Address_Not_Of_The_Country_Named()
    {
        ArrangePassingContext();
        _registry = AresOnly(BusinessRegistryRecord.NotRegistered);

        var result = await CreateValidator().ValidateAsync(Valid() with { BusinessCountryId = SlovakiaId });

        Assert.Equal(BusinessErrorMessage.RegistrationNumberNotRegistered, Assert.Single(result.Errors).ErrorMessage);
    }

    /// <summary>ARES as it answers: for a Czech number only, and nothing consulted for any other country.</summary>
    private static Mock<IBusinessRegistry> AresOnly(BusinessRegistryRecord czechAnswer)
    {
        var registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotConsulted);
        registry
            .Setup(r => r.LookupAsync("CZE", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(czechAnswer);
        return registry;
    }

    public static TheoryData<BusinessRegistryRecord> SaveableAnswers => new()
    {
        BusinessRegistryRecord.Unavailable,
        BusinessRegistryRecord.NotConsulted,
        new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: false),
    };

    [Theory]
    [MemberData(nameof(SaveableAnswers))]
    public async Task Any_Other_Answer_Lets_The_Save_Through(BusinessRegistryRecord record)
    {
        ArrangePassingContext();
        _registry = BusinessRegistryDoubles.Answering(record);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    private const string StoredNumber = "87654321";

    private void ArrangeApprovedCleanerWithAStoredNumber(string storedNumber = StoredNumber)
    {
        ArrangePassingContext();
        _employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, storedNumber, legalEntityName: null);
        _employee.AssignWorkCountry(CountryId);
        _employee.Approve(approvedByUserId: "admin-1");
    }

    public static TheoryData<BusinessRegistryRecord, string> ApprovalRefusals => new()
    {
        { new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true), BusinessErrorMessage.EmployeeBusinessCeased },
        { new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false), BusinessErrorMessage.EmployeeTradeLicenceInactive },
        { BusinessRegistryRecord.Unavailable, BusinessErrorMessage.EmployeeBusinessRegistryUnavailable },
        { BusinessRegistryRecord.NotRegistered, BusinessErrorMessage.RegistrationNumberNotRegistered },
    };

    /// <summary>
    /// Owner ruling 2026-10-05: a cleaner already approved who swaps in another company ID passes approval's
    /// full register check on that save, an outage included, because the new number is one no approval saw.
    /// </summary>
    [Theory]
    [MemberData(nameof(ApprovalRefusals))]
    public async Task An_Approved_Cleaner_Changing_The_Number_Is_Held_To_The_Approval_Check(
        BusinessRegistryRecord record, string refusal)
    {
        ArrangeApprovedCleanerWithAStoredNumber();
        _registry = BusinessRegistryDoubles.Answering(record);

        var result = await CreateValidator().ValidateAsync(Valid());

        var failure = Assert.Single(result.Errors);
        Assert.Equal(refusal, failure.ErrorMessage);
        Assert.Equal(nameof(UpdateIdentificationInfo.Command.RegistrationNumber), failure.PropertyName);
        _registry.Verify(r => r.LookupAsync("CZE", "12345678", It.IsAny<CancellationToken>()), Times.Once);
    }

    public static TheoryData<BusinessRegistryRecord> AnswersOnlyApprovalRefuses => new()
    {
        new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true),
        new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false),
        BusinessRegistryRecord.Unavailable,
    };

    [Theory]
    [MemberData(nameof(AnswersOnlyApprovalRefuses))]
    public async Task An_Approved_Cleaner_Resaving_The_Stored_Number_Is_Judged_Only_On_Existence(BusinessRegistryRecord record)
    {
        ArrangeApprovedCleanerWithAStoredNumber(storedNumber: "12345678");
        _registry = BusinessRegistryDoubles.Answering(record);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(" 12345678")]
    [InlineData("12345678 ")]
    public async Task A_Whitespace_Only_Difference_Is_Not_A_Change(string storedNumber)
    {
        ArrangeApprovedCleanerWithAStoredNumber(storedNumber);
        _registry = BusinessRegistryDoubles.Answering(
            new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true));

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task A_Rejected_Cleaner_Changing_The_Number_Is_Judged_Only_On_Existence()
    {
        ArrangePassingContext();
        _employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, StoredNumber, legalEntityName: null);
        _employee.Reject(rejectedByUserId: "admin-1", reason: "documents");
        _registry = BusinessRegistryDoubles.Answering(
            new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true));

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Approved_Cleaner_Changing_The_Number_Where_No_Register_Is_Asked_Saves()
    {
        ArrangeApprovedCleanerWithAStoredNumber();
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotConsulted);

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Approved_Cleaners_Number_In_The_Wrong_Format_Is_Not_Taken_To_The_Register()
    {
        ArrangeApprovedCleanerWithAStoredNumber();
        _taxIdValidator
            .Setup(v => v.ValidateRegistrationNumberAsync(It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Invalid(BusinessErrorMessage.RegistrationNumberInvalidFormat));

        var result = await CreateValidator().ValidateAsync(Valid());

        Assert.Equal(BusinessErrorMessage.RegistrationNumberInvalidFormat, Assert.Single(result.Errors).ErrorMessage);
        _registry.VerifyNoOtherCalls();
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
