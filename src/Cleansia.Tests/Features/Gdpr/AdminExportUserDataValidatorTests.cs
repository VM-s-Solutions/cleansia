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
/// T-0107 (IDA-SEC-08): the admin GDPR export tool is for customer/employee data-subject
/// requests only. It must refuse to target an administrator (AC3) with the same
/// "cannot target admin via GDPR tool" code as the delete tool, so
/// <see cref="IGdprExportService.BuildAsync"/> is never invoked (no GDPR export row completed).
/// </summary>
public class AdminExportUserDataValidatorTests
{
    private readonly Mock<IUserRepository> _userRepository = new();

    private AdminExportUserData.Validator CreateValidator(params User[] users)
    {
        _userRepository.Setup(r => r.GetAll()).Returns(users.AsQueryable().BuildMock());
        foreach (var user in users)
        {
            _userRepository.Setup(r => r.ExistsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        return new AdminExportUserData.Validator(_userRepository.Object);
    }

    private static User BuildUser(string id, UserProfile profile)
    {
        var user = User.CreateWithPassword($"{id}@example.com", "Password1", "First", "Last", profile);
        user.Id = id;
        return user;
    }

    // AC3 — GDPR export refuses an administrator target with the shared "cannot target admin via GDPR tool" code.
    [Fact]
    public async Task When_Target_Is_Administrator_Then_Fails_With_CannotTargetAdminViaGdprTool()
    {
        var target = BuildUser("target-admin", UserProfile.Administrator);
        var validator = CreateValidator(target);

        var result = await validator.ValidateAsync(new AdminExportUserData.Query(target.Id));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.CannotTargetAdminViaGdprTool);
    }

    // Happy path — a customer target passes (the GDPR export tool's intended use).
    [Fact]
    public async Task When_Target_Is_Customer_Then_Valid()
    {
        var target = BuildUser("target-customer", UserProfile.Customer);
        var validator = CreateValidator(target);

        var result = await validator.ValidateAsync(new AdminExportUserData.Query(target.Id));

        Assert.True(result.IsValid);
    }
}
