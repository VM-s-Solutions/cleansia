using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The validator lets an EXISTING-but-unconfirmed account re-register as an employee, and the handler
/// reuses that account. The reused account must also be upgraded to the Employee profile — otherwise
/// the customer-registered row keeps <c>UserProfile.Customer</c> and <c>PartnerLogin</c>'s profile
/// gate rejects it forever. These pin the upgrade on the reuse branch, the guard that an
/// Administrator is never downgraded by it, and the fresh-user path creating an Employee profile.
/// </summary>
public class RegisterEmployeeProfileUpgradeTests
{
    private const string Email = "existing.user@example.com";
    private const string Password = "Password1!@abc";
    private const string Language = "cs";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    private RegisterEmployee.Handler CreateHandler() => new(
        _cartRepository.Object, _userRepository.Object, _employeeRepository.Object, _pending.Object);

    private static RegisterEmployee.Command Command() =>
        new(Email, Password, "John", "Doe", Language);

    [Fact]
    public async Task Existing_Unconfirmed_Customer_Is_Upgraded_To_Employee_Profile()
    {
        var user = User.CreateWithPassword(Email, Password, "John", "Doe");
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserProfile.Employee, user.Profile);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _employeeRepository.Verify(r => r.Add(It.Is<Employee>(e => e.User == user)), Times.Once);
    }

    [Fact]
    public async Task Existing_Unconfirmed_Administrator_Is_Not_Downgraded()
    {
        var user = User.CreateWithPassword(Email, Password, "John", "Doe", UserProfile.Administrator);
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserProfile.Administrator, user.Profile);
    }

    [Fact]
    public async Task Fresh_User_Is_Created_With_Employee_Profile()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Profile == UserProfile.Employee)), Times.Once);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
        _employeeRepository.Verify(r => r.Add(It.IsAny<Employee>()), Times.AtLeastOnce);
    }
}
