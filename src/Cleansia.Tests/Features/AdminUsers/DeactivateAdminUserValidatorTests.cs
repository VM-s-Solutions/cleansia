using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.AdminUsers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.AdminUsers;

/// <summary>
/// Deactivating an admin user already blocks self-deactivation
/// (<see cref="BusinessErrorMessage.CannotDeactivateSelf"/>); this adds the last-admin guard so the
/// final ACTIVE administrator cannot be deactivated and lock the tenant out of its admin console.
/// Regression: with two or more active admins a non-self, non-last target still passes and the
/// self-guard still fires.
/// </summary>
public class DeactivateAdminUserValidatorTests
{
    private const string CallerId = "caller-admin-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _sessionProvider = new();

    private DeactivateAdminUser.Validator CreateValidator(params User[] users)
    {
        _userRepository.Setup(r => r.GetAll()).Returns(users.AsQueryable().BuildMock());
        _sessionProvider.Setup(s => s.GetUserId()).Returns(CallerId);

        return new DeactivateAdminUser.Validator(_userRepository.Object, _sessionProvider.Object);
    }

    private static User BuildAdmin(string id, bool isActive = true)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", UserProfile.Administrator);
        user.Id = id;
        user.IsActive = isActive;
        return user;
    }

    // The only active administrator cannot be deactivated.
    [Fact]
    public async Task When_Target_Is_The_Only_Active_Admin_Then_Fails_With_CannotDeactivateLastAdmin()
    {
        var lastAdmin = BuildAdmin("the-last-admin");
        var validator = CreateValidator(lastAdmin);

        var result = await validator.ValidateAsync(new DeactivateAdminUser.Command(lastAdmin.Id));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotDeactivateLastAdmin);
    }

    // Boundary — an inactive sibling admin does NOT count toward the active-admin total, so the
    // single ACTIVE admin is still treated as the last one.
    [Fact]
    public async Task When_Only_Other_Admin_Is_Inactive_Then_Fails_With_CannotDeactivateLastAdmin()
    {
        var activeTarget = BuildAdmin("active-admin");
        var inactiveSibling = BuildAdmin("inactive-admin", isActive: false);
        var validator = CreateValidator(activeTarget, inactiveSibling);

        var result = await validator.ValidateAsync(new DeactivateAdminUser.Command(activeTarget.Id));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotDeactivateLastAdmin);
    }

    // Two or more active admins, target is neither caller nor the last admin → passes.
    [Fact]
    public async Task When_Two_Active_Admins_And_Target_Not_Self_Then_Valid()
    {
        var caller = BuildAdmin(CallerId);
        var target = BuildAdmin("target-admin");
        var validator = CreateValidator(caller, target);

        var result = await validator.ValidateAsync(new DeactivateAdminUser.Command(target.Id));

        Assert.True(result.IsValid);
    }

    // Regression — the existing self-guard still fires even when other active admins exist.
    [Fact]
    public async Task When_Target_Is_Self_Then_Fails_With_CannotDeactivateSelf()
    {
        var caller = BuildAdmin(CallerId);
        var otherActiveAdmin = BuildAdmin("other-admin");
        var validator = CreateValidator(caller, otherActiveAdmin);

        var result = await validator.ValidateAsync(new DeactivateAdminUser.Command(CallerId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotDeactivateSelf);
    }
}
