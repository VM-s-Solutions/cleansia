using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// The password-reset flow is Internal-only, on both halves. A Google/Apple row has no password login
/// to recover — <c>LoginValidator</c> has always refused a password against it — so mailing a reset code
/// to one, or writing a password through one, lands a credential that can never be used while the
/// account keeps looking resettable. Each refusal names the provider the account ACTUALLY uses so the
/// caller learns how to sign in; an unknown address must still read as unknown, or the refusal becomes
/// a provider oracle over arbitrary emails.
/// Written red -> green: before the guard every non-Internal case below passed validation.
/// </summary>
public class PasswordResetAuthTypeGuardTests
{
    private const string NewPassword = "BrandNew123";
    private const string UnknownEmail = "nobody@example.com";

    // Any of these appearing for an address that is not known to exist would be the provider leak.
    private static readonly string[] AuthTypeMessages =
    [
        BusinessErrorMessage.GoogleAuthTypeError,
        BusinessErrorMessage.AppleAuthTypeError,
        BusinessErrorMessage.ExternalAuthTypeError,
        BusinessErrorMessage.InternalAuthTypeError,
    ];

    [Theory]
    [InlineData(AuthenticationType.Google, BusinessErrorMessage.GoogleAuthTypeError)]
    [InlineData(AuthenticationType.Apple, BusinessErrorMessage.AppleAuthTypeError)]
    [InlineData((AuthenticationType)99, BusinessErrorMessage.ExternalAuthTypeError)]
    public async Task Requesting_A_Reset_Code_Is_Refused_With_The_Account_Own_Provider(
        AuthenticationType authenticationType, string expectedMessage)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { AuthenticationType = authenticationType });

        var result = await new RequestPasswordChange.Validator(RepoFor(user).Object)
            .ValidateAsync(new RequestPasswordChange.Command(user.Email));

        Assert.False(result.IsValid);
        AssertNamesOnly(result.Errors, expectedMessage, nameof(RequestPasswordChange.Command.Email));
    }

    [Fact]
    public async Task An_Internal_Account_Can_Still_Request_A_Reset_Code()
    {
        var user = UserMockFactory.Generate();

        var result = await new RequestPasswordChange.Validator(RepoFor(user).Object)
            .ValidateAsync(new RequestPasswordChange.Command(user.Email));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Unknown_Email_Still_Reads_As_Unknown_When_Requesting_A_Reset_Code()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(UnknownEmail, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await new RequestPasswordChange.Validator(repo.Object)
            .ValidateAsync(new RequestPasswordChange.Command(UnknownEmail));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
        Assert.DoesNotContain(result.Errors, e => AuthTypeMessages.Contains(e.ErrorMessage));
        // Cascade.Stop puts the guard behind the existence rule, so an unknown address is never read.
        repo.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(AuthenticationType.Google, BusinessErrorMessage.GoogleAuthTypeError)]
    [InlineData(AuthenticationType.Apple, BusinessErrorMessage.AppleAuthTypeError)]
    [InlineData((AuthenticationType)99, BusinessErrorMessage.ExternalAuthTypeError)]
    public async Task Completing_A_Reset_Is_Refused_With_The_Account_Own_Provider(
        AuthenticationType authenticationType, string expectedMessage)
    {
        // The live code models the rows the unguarded request side already produced: the completion
        // half must refuse on its own, not lean on the request half no longer minting codes.
        var (user, rawCode) = SocialAccountHoldingALiveResetCode(authenticationType);

        var result = await new ChangePassword.Validator(RepoFor(user).Object)
            .ValidateAsync(new ChangePassword.Command(user.Email, NewPassword, rawCode));

        Assert.False(result.IsValid);
        AssertNamesOnly(result.Errors, expectedMessage, nameof(ChangePassword.Command.Email));
    }

    [Fact]
    public async Task An_Internal_Account_Can_Still_Complete_A_Reset()
    {
        var (user, rawCode) = SocialAccountHoldingALiveResetCode(AuthenticationType.Internal);

        var result = await new ChangePassword.Validator(RepoFor(user).Object)
            .ValidateAsync(new ChangePassword.Command(user.Email, NewPassword, rawCode));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Unknown_Email_Still_Reads_As_Unknown_When_Completing_A_Reset()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(UnknownEmail, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await new ChangePassword.Validator(repo.Object)
            .ValidateAsync(new ChangePassword.Command(UnknownEmail, NewPassword, "any-code"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotExistingUserWithEmail);
        Assert.DoesNotContain(result.Errors, e => AuthTypeMessages.Contains(e.ErrorMessage));
        repo.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void AssertNamesOnly(
        IEnumerable<FluentValidation.Results.ValidationFailure> errors, string expectedMessage, string errorCode)
    {
        Assert.Contains(errors, e => e.ErrorMessage == expectedMessage && e.ErrorCode == errorCode);
        // The bug the shared resolver exists to prevent: an Apple account told it signed in with Google.
        Assert.DoesNotContain(errors, e => e.ErrorMessage != expectedMessage && AuthTypeMessages.Contains(e.ErrorMessage));
    }

    private static Mock<IUserRepository> RepoFor(User user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.Setup(r => r.TryChargeResetPasswordCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return repo;
    }

    private static (User user, string rawCode) SocialAccountHoldingALiveResetCode(AuthenticationType authenticationType)
    {
        var raw = SecurityTokens.Generate();
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = authenticationType,
            ResetPasswordCode = SecurityTokens.Hash(raw),
            ResetPasswordCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
        });
        return (user, raw);
    }
}
