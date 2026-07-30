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
/// ADR-0001 (ADR-AUTHZ) S1 server-truth-identity, D5 "don't trust client
/// identity". The hole: the Google sign-in handler resolved / provisioned the user by the
/// client-supplied <c>command.Email</c> / <c>command.GoogleId</c> and discarded the verified Google
/// ID-token payload — an account-takeover surface where an attacker posts a victim's email with any
/// token/GoogleId and receives a JWT for that victim.
///
/// The fix moves verification into <see cref="IGoogleTokenVerifier"/> (the single seam that calls
/// Google) and binds identity from the VERIFIED <see cref="GoogleVerifiedClaims"/> (Subject + Email),
/// never the request. These handler tests mock the verifier so they assert the binding/branching:
///   - a verified <c>claims.Email</c> that differs from <c>command.Email</c> wins — the account
///     resolved/provisioned is the token's email, never the attacker-claimed one;
///   - <see cref="User.CreateWithGoogle"/> binds <c>claims.Subject</c>, never <c>command.GoogleId</c>;
///   - a forged/unverifiable token (verifier returns null) fails with
///     <see cref="BusinessErrorMessage.InvalidGoogleUserToken"/> and creates no <see cref="User"/>/<see cref="Cart"/>;
///   - the legitimate flow is preserved (known active user → token; unknown email → provision
///     from verified claims);
///   - an existing account whose verified email collides but whose AuthenticationType is NOT Google
///     (covers BOTH Internal AND Apple) is rejected, and the rejection names the provider that account
///     ACTUALLY uses — an Apple user has no password, so "use email and password" is a dead end;
///   - provisioning happens ONLY when <c>claims.EmailVerified</c> (parity with the AppleAuth gate —
///     an unverified-email Google token provisions NOTHING);
///   - resolving an EXISTING account through the email fallback is gated on <c>claims.EmailVerified</c>
///     too — a token that merely ASSERTS a victim's address never resolves onto their account.
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

    // The email that resolves the account comes from the VERIFIED token, not the request.
    [Fact]
    public async Task Uses_Verified_Email_Not_Request_Email_When_Provisioning()
    {
        const string attackerClaimedEmail = "victim@example.com";
        const string verifiedEmail = "real-google-user@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("verified-subject", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(CommandWith(attackerClaimedEmail, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // The attacker-claimed email is never looked up...
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(attackerClaimedEmail, It.IsAny<CancellationToken>()), Times.Never);
        // ...only the verified email is.
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()), Times.Once);
        // ...and the new account is provisioned with the verified email.
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == verifiedEmail)), Times.Once);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == attackerClaimedEmail)), Times.Never);
    }

    // User.CreateWithGoogle binds claims.Subject, never command.GoogleId.
    [Fact]
    public async Task Uses_Verified_Subject_Not_Request_GoogleId_When_Provisioning()
    {
        const string verifiedSubject = "verified-subject-999";
        const string attackerClaimedGoogleId = "attacker-google-id";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(verifiedSubject, "new-user@example.com", EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(CommandWith("new-user@example.com", attackerClaimedGoogleId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.GoogleId == verifiedSubject)), Times.Once);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.GoogleId == attackerClaimedGoogleId)), Times.Never);
    }

    // Verifier returns null (forged/unverifiable token, or audience mismatch resolved
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

        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Legitimate flow: known active Google user signs in → JwtTokenResponse, no new user/cart.
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
            .ReturnsAsync(new GoogleVerifiedClaims("subject-1", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(CommandWith(existing.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Legitimate flow: unknown verified email → provision User + Cart from verified claims, token issued.
    [Fact]
    public async Task Unknown_Verified_Email_Provisions_User_And_Cart_From_Claims()
    {
        const string verifiedEmail = "brand-new@example.com";
        const string verifiedSubject = "subject-new";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(verifiedSubject, verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
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

    // Parity with the AppleAuth gate: a verified token whose email_verified is false provisions NOTHING.
    [Fact]
    public async Task Unverified_Email_Does_Not_Provision()
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("subject-unverified", "unverified@example.com", EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(CommandWith("unverified@example.com", "any-google-id"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidGoogleUserToken, result.Error!.Message);
        Assert.Equal(nameof(GoogleAuth.Command.Token), result.Error!.Code);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The email is the ONLY thing that resolves an existing account here, so it has to be an email GOOGLE
    // vouched for: a token asserting an address Google never verified must not hand back the JWT of the
    // account registered under it. Unverified → the lookup is not even attempted and the sign-in fails
    // closed exactly like an unprovisionable one.
    [Fact]
    public async Task Unverified_Email_Does_Not_Match_An_Existing_Account()
    {
        var victim = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Google
        });
        victim.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("attacker-subject", victim.Email, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(victim.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(victim);

        var result = await CreateHandler().Handle(CommandWith(victim.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidGoogleUserToken, result.Error!.Message);
        Assert.Equal(nameof(GoogleAuth.Command.Token), result.Error!.Code);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
    }

    // The other half of that gate: it narrows the fallback, it does not remove it. A legitimate returning
    // user whose token DOES report the email as verified still resolves by email and still gets a token.
    [Fact]
    public async Task Verified_Email_Still_Matches_An_Existing_Account()
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Google
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("subject-verified", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(CommandWith(existing.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // (S1): the AuthenticationType guard now runs in the HANDLER against the VERIFIED
    // claims.Email (it used to run in the validator against the attacker-controlled command.Email, so a
    // Google login whose verified email collided with an Internal/password account got a JWT for it).
    // An existing user with the verified email whose type is not Google MUST be rejected — no token, no
    // provisioning — and that holds for the Apple collision as much as the Internal one.
    // Two independent properties, and only the second is new: the account is NEVER bound, and the
    // message names the provider that account ACTUALLY uses rather than always saying "email and
    // password" (which is a dead end for an Apple account, since it has no password to offer).
    [Theory]
    [InlineData(AuthenticationType.Internal, BusinessErrorMessage.InternalAuthTypeError)]
    [InlineData(AuthenticationType.Apple, BusinessErrorMessage.AppleAuthTypeError)]
    public async Task Existing_NonGoogle_Account_With_Verified_Email_Is_Rejected_Naming_Its_Provider(
        AuthenticationType collidingType, string expectedMessage)
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = collidingType
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("subject-collide", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(CommandWith(existing.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedMessage, result.Error!.Message);
        // No JWT issued for the colliding account, and nothing provisioned.
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
    }

    // Inactive existing user is rejected (behavior preserved from the original handler).
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
            .ReturnsAsync(new GoogleVerifiedClaims("subject-1", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(CommandWith(existing.Email, "any-google-id"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidPassword, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The bug this replaces: the account was resolved by EMAIL, a provider-owned attribute the user can
    // change at will. Change the address on the Google account and the row stopped matching, so the next
    // sign-in provisioned a duplicate. The subject is the stable identity and now resolves first.
    [Fact]
    public async Task Existing_Account_Resolves_By_Subject_Even_When_The_Google_Email_Changed()
    {
        const string subject = "stable-subject-1";
        var existing = User.CreateWithGoogle("old-address@example.com", "First", "Last", subject);
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(subject, "changed-address@example.com", EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync(subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(
            CommandWith("changed-address@example.com", subject), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // No duplicate provisioned, and the email lookup never ran — the subject was enough.
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _userRepository.Verify(
            r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // An account provisioned before subjects were stored resolves by email exactly once, and that
    // sign-in anchors it. Without the bind it would take the fragile email path forever.
    [Fact]
    public async Task Email_Fallback_Binds_The_Subject_So_The_Next_Sign_In_Resolves_By_It()
    {
        const string subject = "subject-to-bind";
        const string email = "legacy-account@example.com";
        var legacy = User.CreateWithGoogle(email, "First", "Last", googleId: string.Empty);
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(subject, email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync(subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(legacy);

        var result = await CreateHandler().Handle(CommandWith(email, subject), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(subject, legacy.GoogleId);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }

    // S1 server-truth-identity: a bound subject is the account's verified anchor. Letting a later
    // sign-in rewrite it would let one Google identity take over an account anchored to another.
    [Fact]
    public async Task An_Already_Bound_Subject_Is_Never_Rewritten()
    {
        const string boundSubject = "the-real-owner";
        const string attackerSubject = "someone-else";
        const string email = "shared@example.com";
        var existing = User.CreateWithGoogle(email, "First", "Last", boundSubject);
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(attackerSubject, email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync(attackerSubject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateHandler().Handle(CommandWith(email, attackerSubject), CancellationToken.None);

        Assert.Equal(boundSubject, existing.GoogleId);
    }

    // The A1 gate still holds on the fallback: an unverified email must not reach the email lookup at
    // all, even now that a subject miss is what routes it there.
    [Fact]
    public async Task Unverified_Email_Does_Not_Reach_The_Fallback_After_A_Subject_Miss()
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims("unknown-subject", "victim@example.com", EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            CommandWith("victim@example.com", "unknown-subject"), CancellationToken.None);

        Assert.True(result.IsFailure);
        _userRepository.Verify(
            r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }
}
