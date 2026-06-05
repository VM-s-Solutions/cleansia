using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Users;

/// <summary>
/// Password-reset flow. Owner-decision (BINDING): reset lookup is by
/// (email, HASH of token). The validator loads the user by email and compares
/// <c>user.ResetPasswordCode == SHA-256(command.Code)</c> (no plaintext compare); expiry is enforced;
/// the handler clears the hashed column on success (one-shot).
///
///   - a correct code for the WRONG email fails; a wrong code for the RIGHT email fails; the
///     compare is over the hash, never plaintext.
///   - an expired reset token is rejected; a consumed token clears the hashed column and cannot
///     be replayed.
/// Written red -> green (predates the hash-compare rewrite). New password meets the validator's
/// policy (>=8 chars, letter + digit) so the token rule is the only thing under test.
/// </summary>
public class ChangePasswordSecurityTests
{
    private const string RightEmail = "owner@example.com";
    private const string WrongEmail = "someone-else@example.com";
    private const string NewPassword = "BrandNew123";

    private static User UserWithResetToken(string email, string hashedToken, DateTimeOffset expiresAt)
        => UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            Email = email,
            ResetPasswordCode = hashedToken,
            ResetPasswordCodeExpiresAt = expiresAt,
        });

    private static Mock<IUserRepository> RepoFor(User user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return repo;
    }

    // Correct code, RIGHT email -> passes (the hash matches, expiry live, password differs).
    [Fact]
    public async Task Correct_Code_For_Right_Email_Passes()
    {
        var raw = SecurityTokens.Generate();
        var user = UserWithResetToken(RightEmail, SecurityTokens.Hash(raw), DateTimeOffset.UtcNow.AddMinutes(15));
        var validator = new ChangePassword.Validator(RepoFor(user).Object);

        var result = await validator.ValidateAsync(new ChangePassword.Command(RightEmail, NewPassword, raw));

        Assert.True(result.IsValid);
    }

    // Correct code but WRONG email fails (email binds the lookup).
    [Fact]
    public async Task Correct_Code_For_Wrong_Email_Fails()
    {
        var raw = SecurityTokens.Generate();
        // The token belongs to RightEmail's account; the attacker submits it against WrongEmail.
        var rightUser = UserWithResetToken(RightEmail, SecurityTokens.Hash(raw), DateTimeOffset.UtcNow.AddMinutes(15));
        var wrongUser = UserWithResetToken(WrongEmail, SecurityTokens.Hash(SecurityTokens.Generate()), DateTimeOffset.UtcNow.AddMinutes(15));

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.ExistsWithEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByEmailAsync(RightEmail, It.IsAny<CancellationToken>())).ReturnsAsync(rightUser);
        repo.Setup(r => r.GetByEmailAsync(WrongEmail, It.IsAny<CancellationToken>())).ReturnsAsync(wrongUser);

        var validator = new ChangePassword.Validator(repo.Object);

        var result = await validator.ValidateAsync(new ChangePassword.Command(WrongEmail, NewPassword, raw));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
    }

    // Wrong code, RIGHT email fails (hash mismatch). Proves no plaintext compare survives.
    [Fact]
    public async Task Wrong_Code_For_Right_Email_Fails()
    {
        var realRaw = SecurityTokens.Generate();
        var user = UserWithResetToken(RightEmail, SecurityTokens.Hash(realRaw), DateTimeOffset.UtcNow.AddMinutes(15));
        var validator = new ChangePassword.Validator(RepoFor(user).Object);

        var result = await validator.ValidateAsync(new ChangePassword.Command(RightEmail, NewPassword, "totally-wrong-code"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
    }

    // The stored reset column is NEVER the raw token a plaintext compare would match.
    [Fact]
    public async Task Submitting_The_Stored_Hash_As_The_Code_Does_Not_Pass()
    {
        var raw = SecurityTokens.Generate();
        var hashed = SecurityTokens.Hash(raw);
        var user = UserWithResetToken(RightEmail, hashed, DateTimeOffset.UtcNow.AddMinutes(15));
        var validator = new ChangePassword.Validator(RepoFor(user).Object);

        // If a plaintext compare survived, submitting the stored value would (wrongly) pass. It must not:
        // the validator hashes the input, so it compares Hash(hashed) != hashed.
        var result = await validator.ValidateAsync(new ChangePassword.Command(RightEmail, NewPassword, hashed));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
    }

    [Fact]
    public async Task Expired_Reset_Token_Is_Rejected()
    {
        var raw = SecurityTokens.Generate();
        var user = UserWithResetToken(RightEmail, SecurityTokens.Hash(raw), DateTimeOffset.UtcNow.AddMinutes(-1));
        var validator = new ChangePassword.Validator(RepoFor(user).Object);

        var result = await validator.ValidateAsync(new ChangePassword.Command(RightEmail, NewPassword, raw));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.NotValidResetPasswordToken);
    }

    // One-shot: the handler clears the hashed reset column on success so it cannot be replayed.
    // ChangePassword.Handler is internal; resolve it via reflection (no compile-time reference).
    [Fact]
    public async Task Handler_Clears_Reset_Token_After_Consumption()
    {
        var raw = SecurityTokens.Generate();
        var user = UserWithResetToken(RightEmail, SecurityTokens.Hash(raw), DateTimeOffset.UtcNow.AddMinutes(15));
        var repo = RepoFor(user);

        var result = await InvokeHandler(repo.Object, new ChangePassword.Command(RightEmail, NewPassword, raw));

        Assert.True(result.IsSuccess);
        Assert.Null(user.ResetPasswordCode);                // hashed column cleared (one-shot)
        Assert.Null(user.ResetPasswordCodeExpiresAt);
    }

    private static async Task<BusinessResult<ChangePassword.Response>> InvokeHandler(
        IUserRepository repo, ChangePassword.Command command)
    {
        var handlerType = typeof(ChangePassword).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, repo)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<ChangePassword.Response>>)handleMethod!.Invoke(
            handler, [command, CancellationToken.None])!;
        return await task;
    }
}
