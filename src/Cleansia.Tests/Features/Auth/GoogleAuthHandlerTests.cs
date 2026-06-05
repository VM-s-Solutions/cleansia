using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// T-0105 (IDA-SEC-01) / ADR-0001 (ADR-AUTHZ) S1 server-truth-identity, D5 "don't trust client
/// identity". The hole: the Google sign-in handler resolved / provisioned the user by the
/// client-supplied <c>command.Email</c> / <c>command.GoogleId</c> and discarded the verified Google
/// ID-token payload — an account-takeover surface where an attacker posts a victim's email with any
/// token/GoogleId and receives a JWT for that victim.
///
/// The fix moves verification into <see cref="IGoogleTokenVerifier"/> (the single seam that calls
/// Google) and binds identity from the VERIFIED <see cref="GoogleVerifiedClaims"/> (Subject + Email),
/// never the request. These handler tests mock the verifier so they assert the binding/branching:
///   - AC1: a verified <c>claims.Email</c> that differs from <c>command.Email</c> wins — the account
///     resolved/provisioned is the token's email, never the attacker-claimed one;
///   - AC2: <see cref="User.CreateWithGoogle"/> binds <c>claims.Subject</c>, never <c>command.GoogleId</c>;
///   - AC5: a forged/unverifiable token (verifier returns null) fails with
///     <see cref="BusinessErrorMessage.InvalidGoogleUserToken"/> and creates no <see cref="User"/>/<see cref="Cart"/>;
///   - AC6: the legitimate flow is preserved (known active user → token; unknown email → provision
///     from verified claims).
/// Written red → green per knowledge/testing.md (predates the handler rewrite).
/// </summary>
public class GoogleAuthHandlerTests
{
    private const string HostAudience = "customer";

    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IGoogleTokenVerifier> _verifier = new();
    private readonly IHostAudienceProvider _hostAudience = new HostAudienceProvider(HostAudience);

    public GoogleAuthHandlerTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
    }

    private GoogleAuth.Handler CreateHandler() =>
        (GoogleAuth.Handler)Activator.CreateInstance(
            typeof(GoogleAuth.Handler),
            _verifier.Object,
            _tokenService.Object,
            _cartRepository.Object,
            _userRepository.Object,
            _hostAudience)!;

    private static GoogleAuth.Command CommandWith(string email, string googleId) =>
        new(Token: "any-token", GoogleId: googleId, Email: email, FirstName: "First", LastName: "Last");

    // AC1 — the email that resolves the account comes from the VERIFIED token, not the request.
    [Fact]
    public async Task Uses_Verified_Email_Not_Request_Email_When_Provisioning()
    {
        const string attackerClaimedEmail = "victim@example.com";
        const string verifiedEmail = "real-google-user@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("verified-subject", verifiedEmail));
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(CommandWith(attackerClaimedEmail, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The attacker-claimed email is never looked up...
        _userRepository.Verify(r => r.GetByEmailAsync(attackerClaimedEmail, It.IsAny<CancellationToken>()), Times.Never);
        // ...only the verified email is.
        _userRepository.Verify(r => r.GetByEmailAsync(verifiedEmail, It.IsAny<CancellationToken>()), Times.Once);
        // ...and the new account is provisioned with the verified email.
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == verifiedEmail)), Times.Once);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == attackerClaimedEmail)), Times.Never);
    }

    // AC2 — User.CreateWithGoogle binds claims.Subject, never command.GoogleId.
    [Fact]
    public async Task Uses_Verified_Subject_Not_Request_GoogleId_When_Provisioning()
    {
        const string verifiedSubject = "verified-subject-999";
        const string attackerClaimedGoogleId = "attacker-google-id";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(verifiedSubject, "new-user@example.com"));
        _userRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(CommandWith("new-user@example.com", attackerClaimedGoogleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.GoogleId == verifiedSubject)), Times.Once);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.GoogleId == attackerClaimedGoogleId)), Times.Never);
    }

    // AC3 / AC4 / AC5 — verifier returns null (forged/unverifiable token, or audience mismatch resolved
    // to null inside the verifier) → InvalidGoogleUserToken, no JWT, no User/Cart created.
    [Fact]
    public async Task Forged_Token_Is_Rejected_With_InvalidGoogleUserToken_And_Creates_Nothing()
    {
        _verifier
            .Setup(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleVerifiedClaims?)null);

        var result = await CreateHandler().Handle(CommandWith("victim@example.com", "any-google-id"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidGoogleUserToken, result.Error!.Message);
        Assert.Equal(nameof(GoogleAuth.Command.Token), result.Error!.Code);

        _userRepository.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // AC6 — legitimate flow: known active Google user signs in → JwtTokenResponse, no new user/cart.
    [Fact]
    public async Task Known_Active_User_Gets_Token_Without_Reprovisioning()
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Google
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("subject-1", existing.Email));
        _userRepository
            .Setup(r => r.GetByEmailAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(CommandWith(existing.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC6 — legitimate flow: unknown verified email → provision User + Cart from verified claims, token issued.
    [Fact]
    public async Task Unknown_Verified_Email_Provisions_User_And_Cart_From_Claims()
    {
        const string verifiedEmail = "brand-new@example.com";
        const string verifiedSubject = "subject-new";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(verifiedSubject, verifiedEmail));
        _userRepository
            .Setup(r => r.GetByEmailAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(CommandWith(verifiedEmail, "ignored-google-id"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u =>
            u.Email == verifiedEmail &&
            u.GoogleId == verifiedSubject &&
            u.AuthenticationType == AuthenticationType.Google)), Times.Once);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // AC6 — inactive existing user is rejected (behavior preserved from the original handler).
    [Fact]
    public async Task Inactive_Existing_User_Is_Rejected()
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Google
        });
        existing.IsActive = false;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("subject-1", existing.Email));
        _userRepository
            .Setup(r => r.GetByEmailAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(CommandWith(existing.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidPassword, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
