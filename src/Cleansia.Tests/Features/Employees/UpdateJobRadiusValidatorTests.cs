using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The radius a cleaner may choose. The bounds are the only shape rule — everything else about the
/// value is a preference, including the null that means "no preference, keep the country-wide board".
/// </summary>
public class UpdateJobRadiusValidatorTests
{
    private const string UserEmail = "radius-cleaner@cleansia.cz";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public UpdateJobRadiusValidatorTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-radius";

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employees
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
    }

    private Task<FluentValidation.Results.ValidationResult> ValidateAsync(int? radiusKm) =>
        new UpdateJobRadius.Validator(_employees.Object, _session.Object)
            .ValidateAsync(new UpdateJobRadius.Command(null, radiusKm), CancellationToken.None);

    [Theory]
    [InlineData(JobProximity.MinRadiusKm)]
    [InlineData(50)]
    [InlineData(JobProximity.MaxRadiusKm)]
    public async Task A_Radius_Inside_The_Bounds_Is_Accepted(int radiusKm)
    {
        Assert.True((await ValidateAsync(radiusKm)).IsValid);
    }

    /// <summary>
    /// Clearing the radius is a legitimate choice, not a missing field: it is how a cleaner asks for the
    /// whole country back.
    /// </summary>
    [Fact]
    public async Task Clearing_The_Radius_Is_Accepted()
    {
        Assert.True((await ValidateAsync(null)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(JobProximity.MaxRadiusKm + 1)]
    public async Task A_Radius_Outside_The_Bounds_Is_Refused_With_The_Contract_Code(int radiusKm)
    {
        var result = await ValidateAsync(radiusKm);

        Assert.Contains(
            result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeJobRadiusOutOfRange);
    }
}
