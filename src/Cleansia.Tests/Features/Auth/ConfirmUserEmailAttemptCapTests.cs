using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Per-code attempt cap on the email-confirm surface (ADR-0003 residual risk, bullet 2).
/// Every attempt that RESOLVES an account is charged against that account's budget BEFORE the code's
/// validity is evaluated, so once the budget is spent even the correct live code is refused — a
/// brute-force run cannot convert a late lucky hit into a confirmation. Unresolvable guesses carry no
/// account to charge; they stay bounded by token entropy plus the per-IP window.
/// </summary>
public class ConfirmUserEmailAttemptCapTests
{
    private static (User user, string rawToken) UserWithLiveToken(DateTimeOffset? expiresAt = null)
    {
        var raw = SecurityTokens.Generate();
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            ConfirmationCode = SecurityTokens.Hash(raw),
            ConfirmationCodeExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(15),
            IsEmailConfirmed = false,
        });
        return (user, raw);
    }

    private static Mock<IUserRepository> RepoResolvingByHash(User user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string raw, CancellationToken _) =>
                user.ConfirmationCode == SecurityTokens.Hash(raw) ? user : null);
        return repo;
    }

    private static ConfirmUserEmail.Validator ValidatorFor(Mock<IUserRepository> repo)
        => new(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());

    [Fact]
    public async Task When_The_Budget_Is_Spent_Even_The_Correct_Live_Code_Is_Refused()
    {
        var (user, raw) = UserWithLiveToken();
        var repo = RepoResolvingByHash(user);
        repo.Setup(r => r.TryChargeConfirmationCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await ValidatorFor(repo).ValidateAsync(new ConfirmUserEmail.Command(raw));

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.TooManyAttempts, result.Errors[0].ErrorMessage);
        Assert.Equal(nameof(ConfirmUserEmail.Command.Code), result.Errors[0].ErrorCode);
    }

    [Fact]
    public async Task A_Resolved_Attempt_Is_Charged_And_A_Valid_Code_Passes_Within_Budget()
    {
        var (user, raw) = UserWithLiveToken();
        var repo = RepoResolvingByHash(user);
        repo.Setup(r => r.TryChargeConfirmationCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await ValidatorFor(repo).ValidateAsync(new ConfirmUserEmail.Command(raw));

        Assert.True(result.IsValid);
        repo.Verify(r => r.TryChargeConfirmationCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Expired_Code_Attempt_Is_Charged_And_Rejected_As_Invalid()
    {
        var (user, raw) = UserWithLiveToken(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));
        var repo = RepoResolvingByHash(user);
        repo.Setup(r => r.TryChargeConfirmationCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await ValidatorFor(repo).ValidateAsync(new ConfirmUserEmail.Command(raw));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidConfirmationCode);
        repo.Verify(r => r.TryChargeConfirmationCodeAttemptAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task An_Unresolvable_Guess_Is_Not_Charged_To_Anyone()
    {
        var (user, _) = UserWithLiveToken();
        var repo = RepoResolvingByHash(user);

        var result = await ValidatorFor(repo).ValidateAsync(new ConfirmUserEmail.Command("not-anyones-token"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidConfirmationCode);
        repo.Verify(r => r.TryChargeConfirmationCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
