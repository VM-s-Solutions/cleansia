using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// T-0107 (IDA-SEC-08): the admin GDPR delete tool is for customer/employee data-subject
/// requests only. It must refuse to target an administrator (AC1) and must refuse self-target
/// (AC2) — admins are managed exclusively through the AdminUsers feature. The validator must
/// reject BEFORE <see cref="IGdprDeletionService.DeleteUserAccountAsync"/> can irreversibly
/// anonymize the user.
/// </summary>
public class AdminDeleteUserAccountValidatorTests
{
    private const string CallerId = "caller-admin-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _sessionProvider = new();

    private AdminDeleteUserAccount.Validator CreateValidator(params User[] users)
    {
        _userRepository.Setup(r => r.GetAll()).Returns(users.AsQueryable().BuildMock());
        foreach (var user in users)
        {
            _userRepository.Setup(r => r.ExistsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        _sessionProvider.Setup(s => s.GetUserId()).Returns(CallerId);

        return new AdminDeleteUserAccount.Validator(_userRepository.Object, _sessionProvider.Object);
    }

    private static User BuildUser(string id, UserProfile profile)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", profile);
        user.Id = id;
        return user;
    }

    // AC1 — GDPR delete refuses an administrator target with a stable "cannot target admin via GDPR tool" code.
    [Fact]
    public async Task When_Target_Is_Administrator_Then_Fails_With_CannotTargetAdminViaGdprTool()
    {
        var target = BuildUser("target-admin", UserProfile.Administrator);
        var validator = CreateValidator(target);

        var result = await validator.ValidateAsync(new AdminDeleteUserAccount.Command(target.Id));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotTargetAdminViaGdprTool);
    }

    // AC2 — GDPR delete refuses a self-target with CannotDeleteSelf.
    [Fact]
    public async Task When_Target_Is_Self_Then_Fails_With_CannotDeleteSelf()
    {
        var caller = BuildUser(CallerId, UserProfile.Customer);
        var validator = CreateValidator(caller);

        var result = await validator.ValidateAsync(new AdminDeleteUserAccount.Command(CallerId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotDeleteSelf);
    }

    // Happy path — a non-admin, non-self customer target passes (the GDPR delete tool's intended use).
    [Fact]
    public async Task When_Target_Is_Customer_And_Not_Self_Then_Valid()
    {
        var target = BuildUser("target-customer", UserProfile.Customer);
        var validator = CreateValidator(target);

        var result = await validator.ValidateAsync(new AdminDeleteUserAccount.Command(target.Id));

        Assert.True(result.IsValid);
    }
}
