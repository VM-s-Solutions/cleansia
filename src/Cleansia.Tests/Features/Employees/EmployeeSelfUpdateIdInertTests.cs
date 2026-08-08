using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The identity contract shared by the partner self-service employee writes: the subject is the JWT
/// caller, so the body's <c>EmployeeId</c> is inert (S1, [OWN-DATA]) and the only precondition left is
/// that the caller HAS an employee record.
///
/// The rule these replace compared the session-resolved employee to the CLIENT-supplied id, so it
/// passed only for a caller that already knew the answer — protecting nothing while making the route
/// uncallable for any client that cannot supply it. The handler contract is pinned end-to-end in
/// <c>Cleansia.HostTests</c> (five of these handlers are <c>internal</c>, and only a host test sees
/// MVC's implicit-required rejection of an absent id anyway).
/// </summary>
public class EmployeeSelfUpdateIdInertTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cz";
    private const string ForeignEmployeeId = "emp-somebody-else";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<ITaxIdValidator> _taxIds = new();

    public EmployeeSelfUpdateIdInertTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employees
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _employees
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _taxIds
            .Setup(v => v.ValidateRegistrationNumberAsync(
                It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
        _taxIds
            .Setup(v => v.ValidateVatNumberAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
    }

    private void ArrangeCallerIsNotAnEmployee() => _employees
        .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
        .ReturnsAsync((Employee?)null);

    private Task<ValidationResult> ValidateAsync<TCommand>(IValidator<TCommand> validator, TCommand command) =>
        validator.ValidateAsync(command, CancellationToken.None);

    private Task<ValidationResult> ValidatePersonalInfoAsync(string? employeeId) => ValidateAsync(
        new UpdatePersonalInfo.Validator(_employees.Object, _session.Object),
        new UpdatePersonalInfo.Command(employeeId, "First", "Last", new DateOnly(1990, 1, 1), "+420123456789"));

    private Task<ValidationResult> ValidateAddressInfoAsync(string? employeeId) => ValidateAsync(
        new UpdateAddressInfo.Validator(_countries.Object, _employees.Object, _session.Object),
        new UpdateAddressInfo.Command(employeeId, "Main Street 10", "Prague", "11000", CountryId));

    private Task<ValidationResult> ValidateEmergencyContactAsync(string? employeeId) => ValidateAsync(
        new UpdateEmergencyContact.Validator(_employees.Object, _session.Object),
        new UpdateEmergencyContact.Command(employeeId, "ICE", "+420777000000"));

    private Task<ValidationResult> ValidateAvailabilityAsync(string? employeeId) => ValidateAsync(
        new UpdateAvailability.Validator(_employees.Object, _session.Object),
        new UpdateAvailability.Command(employeeId, new Dictionary<string, List<UpdateAvailability.TimeRangeDto>>
        {
            ["Monday"] = [new UpdateAvailability.TimeRangeDto("09:00", "17:00")],
        }));

    private Task<ValidationResult> ValidateIdentificationInfoAsync(string? employeeId) => ValidateAsync(
        new UpdateIdentificationInfo.Validator(_countries.Object, _employees.Object, _session.Object, _taxIds.Object),
        new UpdateIdentificationInfo.Command(
            employeeId, CountryId, "AB12345", EmployeeEntityType.NaturalPerson, CountryId, "12345678", null, null));

    private Task<ValidationResult> ValidateJobRadiusAsync(string? employeeId) => ValidateAsync(
        new UpdateJobRadius.Validator(_employees.Object, _session.Object),
        new UpdateJobRadius.Command(employeeId, 50));

    public static TheoryData<string> Features =>
        ["PersonalInfo", "AddressInfo", "EmergencyContact", "Availability", "IdentificationInfo", "JobRadius"];

    private Task<ValidationResult> ValidateAsync(string feature, string? employeeId) => feature switch
    {
        "PersonalInfo" => ValidatePersonalInfoAsync(employeeId),
        "AddressInfo" => ValidateAddressInfoAsync(employeeId),
        "EmergencyContact" => ValidateEmergencyContactAsync(employeeId),
        "Availability" => ValidateAvailabilityAsync(employeeId),
        "IdentificationInfo" => ValidateIdentificationInfoAsync(employeeId),
        "JobRadius" => ValidateJobRadiusAsync(employeeId),
        _ => throw new ArgumentOutOfRangeException(nameof(feature)),
    };

    [Theory]
    [MemberData(nameof(Features))]
    public async Task The_Callers_Own_Id_Passes(string feature)
    {
        Assert.True((await ValidateAsync(feature, EmployeeId)).IsValid);
    }

    [Theory]
    [MemberData(nameof(Features))]
    public async Task Another_Cleaners_Id_Passes_Because_The_Id_Is_Never_Read(string feature)
    {
        Assert.True((await ValidateAsync(feature, ForeignEmployeeId)).IsValid);
    }

    /// <summary>The web/absent-id case — the one MVC used to reject before MediatR was reached.</summary>
    [Theory]
    [MemberData(nameof(Features))]
    public async Task No_Id_At_All_Passes(string feature)
    {
        Assert.True((await ValidateAsync(feature, null)).IsValid);
    }

    [Theory]
    [MemberData(nameof(Features))]
    public async Task A_Blank_Id_Passes(string feature)
    {
        Assert.True((await ValidateAsync(feature, string.Empty)).IsValid);
    }

    [Theory]
    [MemberData(nameof(Features))]
    public async Task A_Caller_With_No_Employee_Record_Is_Refused(string feature)
    {
        ArrangeCallerIsNotAnEmployee();

        var result = await ValidateAsync(feature, EmployeeId);

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotAllowedToUpdateEmployee);
    }
}
