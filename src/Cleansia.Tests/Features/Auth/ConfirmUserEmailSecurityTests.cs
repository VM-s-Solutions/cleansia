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
/// T-0106 (IDA-SEC-03) — email-confirm flow. Owner-decision (BINDING): confirm lookup is BY THE HASH
/// OF THE TOKEN ALONE (the 128-bit secret makes this safe and closes AC3 — a token valid for B can't
/// confirm A, and a guessed/short code can't match a 128-bit token's hash). The repository hashes the
/// incoming RAW token and matches <c>user.ConfirmationCode == Hash(raw)</c>.
///
///   - AC3: a token valid for user B does not confirm/log in user A (lookup by hash; the raw token
///     A submits hashes to something the repo only returns for B's hash).
///   - AC5: an expired token is rejected; a consumed token clears the hashed column (one-shot) and
///     cannot confirm again.
/// Written red -> green (predates the hash-lookup rewrite). The mock repo emulates the production
/// behavior: it resolves a user ONLY when the lookup arg equals that user's stored hashed column.
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

    // The production repo hashes the incoming raw token and matches the stored hashed column.
    // We emulate that here so the mock is faithful to the real lookup contract.
    private static Mock<IUserRepository> RepoResolvingByHash(params User[] users)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByConfirmationCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string raw, CancellationToken _) =>
                users.FirstOrDefault(u => u.ConfirmationCode == SecurityTokens.Hash(raw)));
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

    // AC3 — a token valid for user B does not confirm/log in user A. A submits B's raw token? That
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

    // AC5 — expired confirmation token is rejected by the validator.
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

    // AC5 — one-shot: a consumed token clears the hashed column and cannot confirm again.
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
}
