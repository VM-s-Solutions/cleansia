using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using FluentValidation.Results;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The phone on the mobile Personal save: required, and a number <c>User.PhoneNumber</c>'s own attribute
/// accepts, which means international form with its country code. The web profile save keeps the shared
/// not-empty rule only, because the web's own pattern accepts numbers without a country code.
/// </summary>
public class UpdatePersonalInfoValidatorTests
{
    private const string UserEmail = "cleaner@cleansia.cz";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public UpdatePersonalInfoValidatorTests()
    {
        var employee = Employee.CreateWithUser(User.CreateWithPassword(UserEmail, "Password1", "First", "Last"));
        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employees
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
    }

    private Task<ValidationResult> ValidateAsync(string? phone) =>
        new UpdatePersonalInfo.Validator(_employees.Object, _session.Object)
            .ValidateAsync(new UpdatePersonalInfo.Command(null, "First", "Last", new DateOnly(1990, 1, 1), phone));

    [Theory]
    [InlineData("+420777123456")]
    [InlineData("+421912345678")]
    public async Task A_Number_With_Its_Country_Code_Passes(string phone)
    {
        Assert.True((await ValidateAsync(phone)).IsValid);
    }

    [Theory]
    [InlineData("+")]
    [InlineData("+1")]
    [InlineData("+4201")]
    [InlineData("777123456")]
    public async Task A_Number_That_Cannot_Be_Dialled_Is_Refused_On_The_Phone(string phone)
    {
        var failure = Assert.Single((await ValidateAsync(phone)).Errors);

        Assert.Equal(nameof(UpdatePersonalInfo.Command.Phone), failure.PropertyName);
        Assert.Equal(BusinessErrorMessage.InvalidPhoneNumber, failure.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task No_Number_Is_Refused_As_Required_And_Nothing_Else(string? phone)
    {
        var failure = Assert.Single((await ValidateAsync(phone)).Errors);

        Assert.Equal(nameof(UpdatePersonalInfo.Command.Phone), failure.PropertyName);
        Assert.Equal(BusinessErrorMessage.Required, failure.ErrorMessage);
    }
}
