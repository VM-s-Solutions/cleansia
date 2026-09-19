using Cleansia.Core.AppServices.Auditing;
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
/// Role assignment is one audited command an Administrator runs on another administrator: the target
/// must be an administrator, never the caller, the role must exist, and the write is the repository's
/// guarded one — zero rows means the target was the company's last Administrator and the refusal
/// carries that key. The handler records the before/after role as ids and enum names, never a person.
/// </summary>
public class SetAdminRoleTests
{
    private const string CallerId = "caller-admin-1";
    private const string TenantId = "cleansia-cz";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _sessionProvider = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IAuditContext> _auditContext = new();

    public SetAdminRoleTests()
    {
        _sessionProvider.Setup(s => s.GetUserId()).Returns(CallerId);
        _tenantProvider.Setup(t => t.GetCurrentTenantId()).Returns(TenantId);
    }

    private static User BuildAdmin(string id, AdminRole role = AdminRole.Administrator)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", UserProfile.Administrator, adminRole: role);
        user.Id = id;
        return user;
    }

    private static User BuildCustomer(string id)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last");
        user.Id = id;
        return user;
    }

    private SetAdminRole.Validator CreateValidator(params User[] users)
    {
        _userRepository.Setup(r => r.GetAll()).Returns(users.AsQueryable().BuildMock());
        return new SetAdminRole.Validator(_userRepository.Object, _sessionProvider.Object);
    }

    private SetAdminRole.Handler CreateHandler(params User[] users)
    {
        _userRepository.Setup(r => r.GetAll()).Returns(users.AsQueryable().BuildMock());
        return new SetAdminRole.Handler(_userRepository.Object, _tenantProvider.Object, _auditContext.Object);
    }

    [Fact]
    public async Task Validator_Refuses_An_Empty_Target()
    {
        var validator = CreateValidator(BuildAdmin(CallerId));

        var result = await validator.ValidateAsync(new SetAdminRole.Command("", AdminRole.Support));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    [Fact]
    public async Task Validator_Refuses_A_Target_That_Is_Not_An_Administrator()
    {
        var validator = CreateValidator(BuildAdmin(CallerId), BuildCustomer("a-customer"));

        var result = await validator.ValidateAsync(new SetAdminRole.Command("a-customer", AdminRole.Support));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AdminUserNotFound);
    }

    [Fact]
    public async Task Validator_Refuses_The_Caller_Changing_Their_Own_Role()
    {
        var validator = CreateValidator(BuildAdmin(CallerId), BuildAdmin("other-admin"));

        var result = await validator.ValidateAsync(new SetAdminRole.Command(CallerId, AdminRole.Support));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotChangeOwnRole);
    }

    [Fact]
    public async Task Validator_Refuses_A_Role_Outside_The_Enum()
    {
        var validator = CreateValidator(BuildAdmin(CallerId), BuildAdmin("other-admin"));

        var result = await validator.ValidateAsync(new SetAdminRole.Command("other-admin", (AdminRole)99));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidEnumValue);
    }

    [Fact]
    public async Task Validator_Passes_Another_Administrator_And_A_Real_Role()
    {
        var validator = CreateValidator(BuildAdmin(CallerId), BuildAdmin("other-admin"));

        var result = await validator.ValidateAsync(new SetAdminRole.Command("other-admin", AdminRole.Accountant));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Handler_Asks_The_Guarded_Write_For_The_Callers_Company_And_Returns_The_New_Role()
    {
        _userRepository
            .Setup(r => r.DemoteAdministratorIfAnotherRemainsAsync(TenantId, "other-admin", AdminRole.Accountant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var handler = CreateHandler(BuildAdmin(CallerId), BuildAdmin("other-admin", AdminRole.Support));

        var result = await handler.Handle(new SetAdminRole.Command("other-admin", AdminRole.Accountant), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("other-admin", result.Value.Id);
        Assert.Equal(AdminRole.Accountant, result.Value.Role);
        _userRepository.Verify(
            r => r.DemoteAdministratorIfAnotherRemainsAsync(TenantId, "other-admin", AdminRole.Accountant, It.IsAny<CancellationToken>()),
            Times.Once);
        _userRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handler_Records_The_Role_Before_And_After_As_Ids_And_Names_Only()
    {
        _userRepository
            .Setup(r => r.DemoteAdministratorIfAnotherRemainsAsync(TenantId, "other-admin", AdminRole.Accountant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        object? before = null;
        object? after = null;
        _auditContext
            .Setup(a => a.RecordChange("AdminUser", "other-admin", It.IsAny<object>(), It.IsAny<object>(), null))
            .Callback<string, string, object, object, string?>((_, _, b, a, _) => { before = b; after = a; });
        var handler = CreateHandler(BuildAdmin(CallerId), BuildAdmin("other-admin", AdminRole.Support));

        await handler.Handle(new SetAdminRole.Command("other-admin", AdminRole.Accountant), CancellationToken.None);

        Assert.Equal(new SetAdminRole.RoleSnapshot("other-admin", AdminRole.Support), before);
        Assert.Equal(new SetAdminRole.RoleSnapshot("other-admin", AdminRole.Accountant), after);
    }

    [Fact]
    public async Task Handler_Refuses_When_The_Guarded_Write_Touched_No_Row_With_The_Last_Administrator_Key()
    {
        _userRepository
            .Setup(r => r.DemoteAdministratorIfAnotherRemainsAsync(TenantId, "last-admin", AdminRole.Support, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var handler = CreateHandler(BuildAdmin(CallerId, AdminRole.Support), BuildAdmin("last-admin"));

        var result = await handler.Handle(new SetAdminRole.Command("last-admin", AdminRole.Support), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.CannotDemoteLastAdministrator, result.Error!.Message);
        _auditContext.Verify(
            a => a.RecordChange(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string?>()),
            Times.Never);
    }
}
