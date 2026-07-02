using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Email-confirm flow. Owner-decision (BINDING, updated for the typed-OTP restore): the confirmation
/// code the apps render as six digit boxes is a 6-digit OTP, and a 10^6 space is NOT
/// self-authenticating — so the OTP branch resolves the account BY EMAIL and proves the code with a
/// hash compare on that single account (never lookup by bare code), under the per-code attempt budget
/// and expiry. The LEGACY 22-char 128-bit tokens (in-flight emails from before the switch) keep the
/// old lookup BY THE HASH OF THE TOKEN ALONE, which their entropy makes safe.
///
///   - a code/token valid for user B does not confirm/log in user A (OTP: email names the account
///     and B's code fails A's hash compare; legacy: lookup by hash only ever resolves the owner).
///   - a bare 6-digit guess with no email resolves nothing — the global-guessing hole a
///     code-only OTP lookup would open stays closed.
///   - an expired code is rejected; a consumed code clears the hashed column (one-shot) and
///     cannot confirm again.
/// The mock repo emulates the production behavior: hash-resolution ONLY when the lookup arg equals a
/// user's stored hashed column; email-resolution by exact email.
/// </summary>
public class ConfirmUserEmailSecurityTests
{
    private const string HostAudience = "customer";

    // Builds an UNCONFIRMED user (CreateWithPassword leaves IsEmailConfirmed=false) holding a live
    // hashed confirmation token. We don't use UserMockFactory.Generate here because it calls
    // ConfirmEmail() (which would set IsEmailConfirmed=true and clear the token).
    private static (User user, string rawToken) MakeUserWithLiveToken(string email)
    {
        var raw = SecurityTokens.Generate();
        var user = User.CreateWithPassword(email, Cleansia.TestUtilities.Constants.TestUserSession.TestUserPassword, "First", "Last");
        user.Created(email, DateTime.UtcNow);   // stable Id per account
        user.Merge(new UserMockFactory.UserPartial
        {
            ConfirmationCode = SecurityTokens.Hash(raw),          // stored == hash
            ConfirmationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
        });
        return (user, raw);
    }

    // Same as MakeUserWithLiveToken but the live code is a 6-digit OTP (the current generator shape).
    private static (User user, string otp) MakeUserWithLiveOtp(string email)
    {
        var otp = SecurityTokens.GenerateOtp();
        var user = User.CreateWithPassword(email, Cleansia.TestUtilities.Constants.TestUserSession.TestUserPassword, "First", "Last");
        user.Created(email, DateTime.UtcNow);
        user.Merge(new UserMockFactory.UserPartial
        {
            ConfirmationCode = SecurityTokens.Hash(otp),
            ConfirmationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
        });
        return (user, otp);
    }

    // The production repo hashes the incoming raw token and matches the stored hashed column (legacy
    // branch), and resolves the OTP branch by exact email. We emulate both so the mock is faithful.
    private static Mock<IUserRepository> RepoResolvingByHash(params User[] users)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string raw, CancellationToken _) =>
                users.FirstOrDefault(u => u.ConfirmationCode == SecurityTokens.Hash(raw)));
        repo.Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string email, CancellationToken _) =>
                users.FirstOrDefault(u => u.Email == email));
        // Attempt budget available — the per-code cap is covered by ConfirmUserEmailAttemptCapTests.
        repo.Setup(r => r.TryChargeConfirmationCodeAttemptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return repo;
    }

    private static ConfirmUserEmail.Handler CreateHandler(IUserRepository repo)
    {
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, bool _, string _, CancellationToken _) =>
                new JwtTokenResponse(Token: $"jwt-for-{u.Email}", IsEmailConfirmed: true));
        return new ConfirmUserEmail.Handler(tokenService.Object, repo, new HostAudienceProvider(HostAudience));
    }

    // A token valid for user B does not confirm/log in user A. A submits B's raw token? That
    // resolves B (not A). A submits a guessed short code? It hashes to nothing the repo holds -> reject.
    [Fact]
    public async Task Token_Valid_For_Another_Account_Does_Not_Confirm_The_Attacker()
    {
        var (userB, rawB) = MakeUserWithLiveToken("victimB@example.com");
        var (userA, _) = MakeUserWithLiveToken("attackerA@example.com");
        var repo = RepoResolvingByHash(userA, userB);

        // Attacker A submits a SHORT GUESSED code (the old 6-digit space) — cannot match any 128-bit hash.
        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var guessResult = await validator.ValidateAsync(new ConfirmUserEmail.Command("123456"));
        Assert.False(guessResult.IsValid);
        Assert.Contains(guessResult.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidConfirmationCode);

        // If the attacker somehow submits B's raw token, it confirms B (its owner) — NEVER A.
        var result = await CreateHandler(repo.Object).Handle(new ConfirmUserEmail.Command(rawB), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal($"jwt-for-{userB.Email}", result.Value.Token);
        Assert.True(userB.IsEmailConfirmed);
        Assert.False(userA.IsEmailConfirmed);   // A is never confirmed by B's token
    }

    // Expired confirmation token is rejected by the validator.
    [Fact]
    public async Task Expired_Confirmation_Token_Is_Rejected()
    {
        var raw = SecurityTokens.Generate();
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            ConfirmationCode = SecurityTokens.Hash(raw),
            ConfirmationCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1),  // expired
            IsEmailConfirmed = false,
        });
        var repo = RepoResolvingByHash(user);
        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());

        var result = await validator.ValidateAsync(new ConfirmUserEmail.Command(raw));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvalidConfirmationCode, result.Errors[0].ErrorMessage);
    }

    // One-shot: a consumed token clears the hashed column and cannot confirm again.
    [Fact]
    public async Task Consumed_Confirmation_Token_Is_Cleared_And_Cannot_Be_Replayed()
    {
        var (user, raw) = MakeUserWithLiveToken("once@example.com");
        var repo = RepoResolvingByHash(user);

        var first = await CreateHandler(repo.Object).Handle(new ConfirmUserEmail.Command(raw), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Null(user.ConfirmationCode);                 // hashed column cleared (one-shot)
        Assert.True(user.IsEmailConfirmed);

        // Replay: the same raw token now hashes to something the repo no longer holds -> rejected.
        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var replay = await validator.ValidateAsync(new ConfirmUserEmail.Command(raw));
        Assert.False(replay.IsValid);
        Assert.Equal(BusinessErrorMessage.InvalidConfirmationCode, replay.Errors[0].ErrorMessage);
    }

    // ---- The 6-digit OTP branch (email-scoped verification) ----

    // Happy path: the account named by email + its live OTP validate and confirm.
    [Fact]
    public async Task Otp_With_Email_Validates_And_Confirms_The_Named_Account()
    {
        var (user, otp) = MakeUserWithLiveOtp("typed@example.com");
        var repo = RepoResolvingByHash(user);

        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var validation = await validator.ValidateAsync(new ConfirmUserEmail.Command(otp, user.Email));
        Assert.True(validation.IsValid);

        var result = await CreateHandler(repo.Object).Handle(new ConfirmUserEmail.Command(otp, user.Email), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.True(user.IsEmailConfirmed);
        Assert.Equal($"jwt-for-{user.Email}", result.Value.Token);
    }

    // A bare 6-digit code with no email resolves nothing — the global-guessing hole stays closed
    // even when the guessed value IS some account's live code.
    [Fact]
    public async Task Otp_Without_Email_Is_Rejected_Even_When_Some_Account_Holds_That_Code()
    {
        var (user, otp) = MakeUserWithLiveOtp("holder@example.com");
        var repo = RepoResolvingByHash(user);
        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());

        var result = await validator.ValidateAsync(new ConfirmUserEmail.Command(otp));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required
            && e.ErrorCode == nameof(ConfirmUserEmail.Command.Email));
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidConfirmationCode);
        Assert.False(user.IsEmailConfirmed);
    }

    // B's live OTP submitted against A's email fails A's hash compare — a code only proves
    // possession relative to the account it was issued to.
    [Fact]
    public async Task Otp_Valid_For_Another_Account_Does_Not_Confirm_The_Attacker()
    {
        var (userA, _) = MakeUserWithLiveOtp("attackerA@example.com");
        var (userB, otpB) = MakeUserWithLiveOtp("victimB@example.com");
        var repo = RepoResolvingByHash(userA, userB);
        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());

        var result = await validator.ValidateAsync(new ConfirmUserEmail.Command(otpB, userA.Email));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.InvalidConfirmationCode, result.Errors[0].ErrorMessage);
        Assert.False(userA.IsEmailConfirmed);
        Assert.False(userB.IsEmailConfirmed);
    }

    // One-shot for the OTP branch: consumption clears the hash; the same email+code cannot replay.
    [Fact]
    public async Task Consumed_Otp_Cannot_Be_Replayed()
    {
        var (user, otp) = MakeUserWithLiveOtp("once-otp@example.com");
        var repo = RepoResolvingByHash(user);

        var first = await CreateHandler(repo.Object).Handle(new ConfirmUserEmail.Command(otp, user.Email), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Null(user.ConfirmationCode);

        var validator = new ConfirmUserEmail.Validator(repo.Object, Mock.Of<ILogger<ConfirmUserEmail.Validator>>());
        var replay = await validator.ValidateAsync(new ConfirmUserEmail.Command(otp, user.Email));
        Assert.False(replay.IsValid);
        Assert.Equal(BusinessErrorMessage.InvalidConfirmationCode, replay.Errors[0].ErrorMessage);
    }
}
