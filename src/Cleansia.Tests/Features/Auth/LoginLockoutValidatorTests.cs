using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Per-account login lockout. The guard keys on the account row, not the caller's
/// IP, so a distributed (multi-IP) credential-stuffing run is bounded by the same per-account budget.
/// All three internal-auth login surfaces (customer/admin/partner) enforce the identical rules.
/// </summary>
public class LoginLockoutValidatorTests
{
    private const string Password = TestUtilities.Constants.TestUserSession.TestUserPassword;

    private static User UserWith(DateTimeOffset? lockoutEndsAt = null)
        => UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            Password = Password.HashAndSaltPassword(),
            LockoutEndsAt = lockoutEndsAt,
        });

    private static Mock<IUserRepository> RepoFor(User user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return repo;
    }

    // These suites assert the no-trusted-device path (T-0193), so the refresh-token store is empty and
    // is never consulted unless a token is presented — the bypass (T-0233) cannot fire here.
    private static IRefreshTokenRepository NoTokens()
    {
        var repo = new Mock<IRefreshTokenRepository>();
        repo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity?)null);
        return repo.Object;
    }

    private static IRefreshTokenService Hasher() => Mock.Of<IRefreshTokenService>();

    private static Login.Validator LoginValidatorFor(Mock<IUserRepository> repo)
        => new(repo.Object, NoTokens(), Hasher());

    private static AdminLogin.Validator AdminValidatorFor(Mock<IUserRepository> repo)
        => new(repo.Object, NoTokens(), Hasher());

    private static PartnerLogin.Validator PartnerValidatorFor(Mock<IUserRepository> repo)
        => new(repo.Object, NoTokens(), Hasher());

    [Fact]
    public async Task When_The_Account_Is_Locked_Even_The_Correct_Password_Is_Refused()
    {
        var user = UserWith(lockoutEndsAt: DateTimeOffset.UtcNow.AddMinutes(10));
        var repo = RepoFor(user);
        var validator = LoginValidatorFor(repo);

        var result = await validator.ValidateAsync(new Login.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked && e.ErrorCode == nameof(Login.Command.Password));
        repo.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_The_Account_Is_Locked_A_Wrong_Password_Is_Not_Evaluated_Or_Recorded()
    {
        var user = UserWith(lockoutEndsAt: DateTimeOffset.UtcNow.AddMinutes(10));
        var repo = RepoFor(user);
        var validator = LoginValidatorFor(repo);

        var result = await validator.ValidateAsync(new Login.Command(user.Email, Password + "wrong", true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPassword);
        repo.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_The_Password_Is_Wrong_The_Failure_Is_Recorded_Against_The_Account()
    {
        var user = UserWith();
        var repo = RepoFor(user);
        var validator = LoginValidatorFor(repo);

        var result = await validator.ValidateAsync(new Login.Command(user.Email, Password + "wrong", true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidPassword);
        repo.Verify(r => r.RecordFailedLoginAsync(user.Email, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task When_The_Password_Is_Correct_Nothing_Is_Recorded_And_Validation_Passes()
    {
        var user = UserWith();
        var repo = RepoFor(user);
        var validator = LoginValidatorFor(repo);

        var result = await validator.ValidateAsync(new Login.Command(user.Email, Password, true));

        Assert.True(result.IsValid);
        repo.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task When_The_Lockout_Window_Has_Expired_The_Correct_Password_Passes()
    {
        var user = UserWith(lockoutEndsAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var validator = LoginValidatorFor(RepoFor(user));

        var result = await validator.ValidateAsync(new Login.Command(user.Email, Password, true));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task AdminLogin_Refuses_A_Locked_Account()
    {
        var user = UserWith(lockoutEndsAt: DateTimeOffset.UtcNow.AddMinutes(10));
        var repo = RepoFor(user);
        var validator = AdminValidatorFor(repo);

        var result = await validator.ValidateAsync(new AdminLogin.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        repo.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdminLogin_Records_A_Wrong_Password()
    {
        var user = UserWith();
        var repo = RepoFor(user);
        var validator = AdminValidatorFor(repo);

        var result = await validator.ValidateAsync(new AdminLogin.Command(user.Email, Password + "wrong", true));

        Assert.False(result.IsValid);
        repo.Verify(r => r.RecordFailedLoginAsync(user.Email, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PartnerLogin_Refuses_A_Locked_Account()
    {
        var user = UserWith(lockoutEndsAt: DateTimeOffset.UtcNow.AddMinutes(10));
        var repo = RepoFor(user);
        var validator = PartnerValidatorFor(repo);

        var result = await validator.ValidateAsync(new PartnerLogin.Command(user.Email, Password, true));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.AccountLocked);
        repo.Verify(r => r.RecordFailedLoginAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartnerLogin_Records_A_Wrong_Password()
    {
        var user = UserWith();
        var repo = RepoFor(user);
        var validator = PartnerValidatorFor(repo);

        var result = await validator.ValidateAsync(new PartnerLogin.Command(user.Email, Password + "wrong", true));

        Assert.False(result.IsValid);
        repo.Verify(r => r.RecordFailedLoginAsync(user.Email, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
