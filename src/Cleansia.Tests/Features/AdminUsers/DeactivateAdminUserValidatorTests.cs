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
/// final ACTIVE Administrator-role administrator cannot be deactivated and lock the tenant out of its
/// admin console — a remaining Support or Accountant does not count, since neither can assign a role or
/// create an account. Regression: with two or more active Administrators a non-self, non-last target
/// still passes and the self-guard still fires.
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

    private static User BuildAdmin(string id, bool isActive = true, AdminRole role = AdminRole.Administrator)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", UserProfile.Administrator, adminRole: role);
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

    // A remaining Support is not an Administrator: the only Administrator-role admin is still the last one.
    [Fact]
    public async Task When_Only_Other_Active_Admin_Is_A_Support_Then_Fails_With_CannotDeactivateLastAdmin()
    {
        var administrator = BuildAdmin("the-administrator");
        var support = BuildAdmin(CallerId, role: AdminRole.Support);
        var validator = CreateValidator(administrator, support);

        var result = await validator.ValidateAsync(new DeactivateAdminUser.Command(administrator.Id));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotDeactivateLastAdmin);
    }

    // Deactivating a Support while an Administrator remains passes: the guard counts Administrators, not the target.
    [Fact]
    public async Task When_Target_Is_A_Support_And_An_Administrator_Remains_Then_Valid()
    {
        var administrator = BuildAdmin(CallerId);
        var support = BuildAdmin("a-support", role: AdminRole.Support);
        var validator = CreateValidator(administrator, support);

        var result = await validator.ValidateAsync(new DeactivateAdminUser.Command(support.Id));

        Assert.True(result.IsValid);
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
