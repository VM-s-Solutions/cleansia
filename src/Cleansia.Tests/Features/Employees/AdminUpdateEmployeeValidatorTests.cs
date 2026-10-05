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
/// An administrator's edit of a cleaner's company ID (IČO) is judged only when it changes the stored number:
/// the format of the register country first, then that country's register, held to approval's full check
/// when the cleaner is approved. The admin web resends the stored number with every section it saves, so
/// an untouched number is never checked.
/// </summary>
public class AdminUpdateEmployeeValidatorTests
{
    private const string EmployeeId = "emp-admin-edit";
    private const string CzechiaId = "country-cz-admin-edit";
    private const string SlovakiaId = "country-sk-admin-edit";
    private const string StoredNumber = "87654321";
    private const string NewNumber = "27082440";

    private static readonly BusinessRegistryRecord Ceased =
        new(BusinessRegistryAnswer.Registered, Ceased: true, TradeLicenceActive: true);

    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ITaxIdValidator> _taxIds = new();
    private Mock<IBusinessRegistry> _registry = BusinessRegistryDoubles.Answering(BusinessRegistryDoubles.InForce());
    private readonly Employee _employee;

    public AdminUpdateEmployeeValidatorTests()
    {
        var user = User.CreateWithPassword("admin-edit@cleansia.test", "Password1", "Jana", "Nováková");
        _employee = Employee.CreateWithUser(user);
        _employee.Id = EmployeeId;
        _employee.UpdateBusinessIdentity(EmployeeEntityType.NaturalPerson, StoredNumber, legalEntityName: null);
        _employee.UpdateAddress(Address.Create("Hlavni 1", "Praha", "11000", CzechiaId));

        _employees.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);

        _countries.Setup(r => r.ExistsAsync(CzechiaId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.GetByIdAsync(CzechiaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Czechia", "CZE", "CZ", isServiced: true));
        _countries.Setup(r => r.ExistsAsync(SlovakiaId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.GetByIdAsync(SlovakiaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Country.Create("Slovakia", "SVK", "SK", isServiced: true));

        _taxIds
            .Setup(v => v.ValidateRegistrationNumberAsync(
                It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
    }

    private AdminUpdateEmployee.Validator CreateValidator() =>
        new(_countries.Object, _employees.Object, _taxIds.Object, _registry.Object);

    private static AdminUpdateEmployee.Command Edit(string? registrationNumber, string? countryId = null) => new(
        EmployeeId: EmployeeId,
        FirstName: "Jana",
        LastName: "Nováková",
        BirthDate: default,
        Phone: null,
        Street: null,
        City: null,
        ZipCode: null,
        CountryId: countryId,
        State: null,
        NationalityId: null,
        PassportId: null,
        EntityType: null,
        RegistrationNumber: registrationNumber,
        LegalEntityName: null,
        EmergencyName: null,
        EmergencyPhone: null);

    private void Approve()
    {
        _employee.AssignWorkCountry(CzechiaId);
        _employee.Approve(approvedByUserId: "admin-1");
    }

    private void FormatRefuses() => _taxIds
        .Setup(v => v.ValidateRegistrationNumberAsync(
            It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(TaxIdValidationResult.Invalid(BusinessErrorMessage.RegistrationNumberInvalidFormat));

    private static void AssertRefusedOnTheNumber(FluentValidation.Results.ValidationResult result, string refusal)
    {
        var failure = Assert.Single(result.Errors);
        Assert.Equal(refusal, failure.ErrorMessage);
        Assert.Equal(nameof(AdminUpdateEmployee.Command.RegistrationNumber), failure.PropertyName);
    }

    [Fact]
    public async Task A_Changed_Number_In_The_Wrong_Format_Is_Refused_And_Not_Taken_To_The_Register()
    {
        FormatRefuses();

        var result = await CreateValidator().ValidateAsync(Edit("1234"));

        AssertRefusedOnTheNumber(result, BusinessErrorMessage.RegistrationNumberInvalidFormat);
        _registry.VerifyNoOtherCalls();
    }

    /// <summary>The country requires a company ID, so an administrator cannot clear it.</summary>
    [Fact]
    public async Task Clearing_A_Number_The_Country_Requires_Is_Refused()
    {
        _taxIds
            .Setup(v => v.ValidateRegistrationNumberAsync(
                CzechiaId, It.IsAny<EmployeeEntityType>(), "", It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Invalid("validation.registration_number.required"));

        var result = await CreateValidator().ValidateAsync(Edit(""));

        AssertRefusedOnTheNumber(result, BusinessErrorMessage.RegistrationNumberInvalidFormat);
    }

    [Fact]
    public async Task A_Pending_Cleaners_Changed_Number_The_Register_Does_Not_Hold_Is_Refused()
    {
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotRegistered);

        var result = await CreateValidator().ValidateAsync(Edit(NewNumber));

        AssertRefusedOnTheNumber(result, BusinessErrorMessage.RegistrationNumberNotRegistered);
        _registry.Verify(r => r.LookupAsync("CZE", NewNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    public static TheoryData<BusinessRegistryRecord, string> ApprovalRefusals => new()
    {
        { Ceased, BusinessErrorMessage.EmployeeBusinessCeased },
        { new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false), BusinessErrorMessage.EmployeeTradeLicenceInactive },
        { BusinessRegistryRecord.Unavailable, BusinessErrorMessage.EmployeeBusinessRegistryUnavailable },
        { BusinessRegistryRecord.NotRegistered, BusinessErrorMessage.RegistrationNumberNotRegistered },
    };

    [Theory]
    [MemberData(nameof(ApprovalRefusals))]
    public async Task An_Approved_Cleaners_Changed_Number_Is_Held_To_The_Approval_Check(
        BusinessRegistryRecord record, string refusal)
    {
        Approve();
        _registry = BusinessRegistryDoubles.Answering(record);

        var result = await CreateValidator().ValidateAsync(Edit(NewNumber));

        AssertRefusedOnTheNumber(result, refusal);
    }

    /// <summary>One number is judged by one country's rules: the work country's, whatever address the edit names.</summary>
    [Fact]
    public async Task An_Approved_Cleaner_Is_Judged_In_The_Work_Country_Whatever_Address_Country_The_Edit_Names()
    {
        Approve();

        await CreateValidator().ValidateAsync(Edit(NewNumber, countryId: SlovakiaId));

        _taxIds.Verify(v => v.ValidateRegistrationNumberAsync(
            CzechiaId, It.IsAny<EmployeeEntityType>(), NewNumber, It.IsAny<CancellationToken>()), Times.Once);
        _registry.Verify(r => r.LookupAsync("CZE", NewNumber, It.IsAny<CancellationToken>()), Times.Once);
        _registry.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(SlovakiaId, "SVK")]
    [InlineData(null, "CZE")]
    public async Task A_Cleaner_With_No_Work_Country_Is_Judged_In_The_Address_Country_Being_Saved_Else_The_Stored_One(
        string? editedCountryId, string askedRegister)
    {
        var result = await CreateValidator().ValidateAsync(Edit(NewNumber, countryId: editedCountryId));

        Assert.True(result.IsValid);
        _registry.Verify(r => r.LookupAsync(askedRegister, NewNumber, It.IsAny<CancellationToken>()), Times.Once);
        _registry.VerifyNoOtherCalls();
    }

    public static TheoryData<BusinessRegistryRecord> AnswersOnlyApprovalRefuses => new()
    {
        Ceased,
        new BusinessRegistryRecord(BusinessRegistryAnswer.Registered, Ceased: false, TradeLicenceActive: false),
        BusinessRegistryRecord.Unavailable,
    };

    [Theory]
    [MemberData(nameof(AnswersOnlyApprovalRefuses))]
    public async Task A_Pending_Cleaners_Changed_Number_Is_Judged_Only_On_Existence(BusinessRegistryRecord record)
    {
        _registry = BusinessRegistryDoubles.Answering(record);

        var result = await CreateValidator().ValidateAsync(Edit(NewNumber));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Approved_Cleaners_Changed_Number_Where_No_Register_Is_Asked_Is_Accepted()
    {
        Approve();
        _registry = BusinessRegistryDoubles.Answering(BusinessRegistryRecord.NotConsulted);

        var result = await CreateValidator().ValidateAsync(Edit(NewNumber));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(StoredNumber)]
    [InlineData(" " + StoredNumber + " ")]
    public async Task An_Unchanged_Number_Is_Never_Checked(string resent)
    {
        Approve();
        FormatRefuses();
        _registry = BusinessRegistryDoubles.Answering(Ceased);

        var result = await CreateValidator().ValidateAsync(Edit(resent));

        Assert.True(result.IsValid);
        _taxIds.VerifyNoOtherCalls();
        _registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Number_The_Edit_Does_Not_Carry_Is_Never_Checked()
    {
        FormatRefuses();

        var result = await CreateValidator().ValidateAsync(Edit(null));

        Assert.True(result.IsValid);
        _taxIds.VerifyNoOtherCalls();
        _registry.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Number_Over_Fifty_Characters_Is_Refused_On_Its_Length_Alone()
    {
        FormatRefuses();

        var result = await CreateValidator().ValidateAsync(Edit(new string('1', 51)));

        AssertRefusedOnTheNumber(result, BusinessErrorMessage.MaxLengthExceeded);
        _registry.VerifyNoOtherCalls();
    }
}
