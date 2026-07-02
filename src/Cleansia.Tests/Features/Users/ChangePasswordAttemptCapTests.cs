using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// Per-code attempt cap on the password-reset surface. The reset command is
/// email-bound, so every guess against an account with an ACTIVE reset code is charged to that
/// account's per-code budget before the code is compared — a wrong-code spray exhausts the budget and
/// the (N+1)-th attempt is refused even if it carries the correct code.
/// </summary>
public class ChangePasswordAttemptCapTests
{
    private const string Email = "owner@example.com";
    private const string NewPassword = "BrandNew123";

    private static (User user, string rawCode) UserWithActiveResetCode()
    {
        var raw = SecurityTokens.Generate();
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            Email = Email,
            ResetPasswordCode = SecurityTokens.Hash(raw),
            ResetPasswordCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
        });
        return (user, raw);
    }

    private static Mock<IUserRepository> RepoFor(User user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return repo;
    }

    [Fact]
    public async Task When_The_Budget_Is_Spent_Even_The_Correct_Code_Is_Refused()
    {
        var (user, raw) = UserWithActiveResetCode();
        var repo = RepoFor(user);
        repo.Setup(r => r.TryChargeResetPasswordCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await new ChangePassword.Validator(repo.Object)
            .ValidateAsync(new ChangePassword.Command(Email, NewPassword, raw));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.TooManyAttempts && e.ErrorCode == nameof(ChangePassword.Command.Code));
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
    }

    [Fact]
    public async Task A_Wrong_Code_Attempt_Is_Charged_To_The_Account()
    {
        var (user, _) = UserWithActiveResetCode();
        var repo = RepoFor(user);
        repo.Setup(r => r.TryChargeResetPasswordCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new ChangePassword.Validator(repo.Object)
            .ValidateAsync(new ChangePassword.Command(Email, NewPassword, "totally-wrong-code"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
        repo.Verify(r => r.TryChargeResetPasswordCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Account_Without_An_Active_Reset_Code_Is_Not_Charged()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Email = Email });
        var repo = RepoFor(user);

        var result = await new ChangePassword.Validator(repo.Object)
            .ValidateAsync(new ChangePassword.Command(Email, NewPassword, "any-code"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
        repo.Verify(r => r.TryChargeResetPasswordCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task The_Correct_Code_Within_Budget_Passes()
    {
        var (user, raw) = UserWithActiveResetCode();
        var repo = RepoFor(user);
        repo.Setup(r => r.TryChargeResetPasswordCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await new ChangePassword.Validator(repo.Object)
            .ValidateAsync(new ChangePassword.Command(Email, NewPassword, raw));

        Assert.True(result.IsValid);
        repo.Verify(r => r.TryChargeResetPasswordCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
