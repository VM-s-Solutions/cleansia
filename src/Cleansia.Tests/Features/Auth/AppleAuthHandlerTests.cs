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
/// ADR-0001 S1 server-truth-identity, D5 "don't trust client identity" — the Apple analogue of
/// <see cref="GoogleAuthHandlerTests"/>. Identity (email + sub +
/// email_verified) is bound from the VERIFIED Apple identity token via <see cref="IAppleTokenVerifier"/>,
/// never the client-supplied request fields. These handler tests mock the verifier so they assert the
/// binding/branching:
///   - a verified <c>claims.Email</c> that differs from any client field wins — the account
///     resolved/provisioned is the token's email;
///   - <see cref="User.CreateWithApple"/> binds <c>claims.Subject</c> into <c>User.AppleId</c>;
///   - a forged/unverifiable token (verifier returns null) fails with
///     <see cref="BusinessErrorMessage.InvalidAppleUserToken"/> and creates no <see cref="User"/>/<see cref="Cart"/>;
///   - an existing account whose verified email collides but whose AuthenticationType is NOT Apple
///     (covers BOTH Internal AND Google) is rejected with <see cref="BusinessErrorMessage.InternalAuthTypeError"/>
///     — closing the verified-email-collision takeover for Apple exactly as Google's hardening did;
///   - provisioning happens ONLY when <c>claims.EmailVerified</c> (stricter than Google today).
/// Written red → green per knowledge/testing.md (the contract precedes the handler body).
/// </summary>
public class AppleAuthHandlerTests
{
    private const string HostAudience = "customer";

    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IAppleTokenVerifier> _verifier = new();
    private readonly IHostAudienceProvider _hostAudience = new HostAudienceProvider(HostAudience);

    public AppleAuthHandlerTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
    }

    private AppleAuth.Handler CreateHandler() =>
        (AppleAuth.Handler)Activator.CreateInstance(
            typeof(AppleAuth.Handler),
            _verifier.Object,
            _tokenService.Object,
            _cartRepository.Object,
            _userRepository.Object,
            _hostAudience)!;

    private static AppleAuth.Command Command() =>
        new(IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "First", LastName: "Last");

    // The email/sub that resolve the account come from the VERIFIED token, never the request.
    [Fact]
    public async Task Uses_Verified_Email_And_Subject_When_Provisioning()
    {
        const string verifiedEmail = "real-apple-user@example.com";
        const string verifiedSubject = "verified-apple-sub-001";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(verifiedSubject, verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.Add(It.Is<User>(u =>
            u.Email == verifiedEmail &&
            u.AppleId == verifiedSubject &&
            u.AuthenticationType == AuthenticationType.Apple)), Times.Once);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Verifier returns null (forged/unverifiable token, audience/issuer/nonce mismatch resolved to null
    // inside the verifier) → InvalidAppleUserToken, no JWT, no User/Cart created.
    [Fact]
    public async Task Forged_Token_Is_Rejected_With_InvalidAppleUserToken_And_Creates_Nothing()
    {
        _verifier
            .Setup(v => v.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppleVerifiedClaims?)null);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidAppleUserToken, result.Error!.Message);
        Assert.Equal(nameof(AppleAuth.Command.IdentityToken), result.Error!.Code);

        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Legitimate flow: known active Apple user signs in → JwtTokenResponse, no new user/cart.
    [Fact]
    public async Task Known_Active_Apple_User_Gets_Token_Without_Reprovisioning()
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Apple
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-1", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

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
        const string verifiedSubject = "apple-sub-new";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(verifiedSubject, verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u =>
            u.Email == verifiedEmail &&
            u.AppleId == verifiedSubject &&
            u.AuthenticationType == AuthenticationType.Apple)), Times.Once);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The verified-email-collision takeover guard runs in the HANDLER against the VERIFIED claims.Email.
    // An Apple login MUST NOT bind into an existing account whose AuthenticationType is not Apple. This
    // covers BOTH the Internal (password) and the Google collision.
    [Theory]
    [InlineData(AuthenticationType.Internal)]
    [InlineData(AuthenticationType.Google)]
    public async Task Existing_NonApple_Account_With_Verified_Email_Is_Rejected_With_InternalAuthTypeError(AuthenticationType collidingType)
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = collidingType
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-collide", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InternalAuthTypeError, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
    }

    // Stricter than Google today: a verified token whose email_verified is false provisions NOTHING.
    [Fact]
    public async Task Unverified_Email_Does_Not_Provision()
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-unverified", "unverified@example.com", EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidAppleUserToken, result.Error!.Message);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Regression (the reported bug): Apple omits the family name on the first authorization, so the
    // command arrives with a null LastName. Provisioning must succeed and store an empty last name — never
    // fail with "last name is required".
    [Fact]
    public async Task Missing_LastName_Provisions_User_With_Empty_LastName()
    {
        const string verifiedEmail = "no-last-name@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-nolast", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Jane", LastName: null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u =>
            u.Email == verifiedEmail &&
            u.FirstName == "Jane" &&
            u.LastName == string.Empty &&
            u.AuthenticationType == AuthenticationType.Apple)), Times.Once);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
    }

    // A later Apple sign-in that provisions (e.g. the account was deleted and recreated) carries no name
    // at all — both fields null. Provisioning must still succeed with empty names.
    [Fact]
    public async Task Missing_Both_Names_Provisions_User_With_Empty_Names()
    {
        const string verifiedEmail = "no-name@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-noname", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: null, LastName: null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u =>
            u.Email == verifiedEmail &&
            u.FirstName == string.Empty &&
            u.LastName == string.Empty)), Times.Once);
    }

    // When Apple folds the whole name into the given-name field and sends no family name, split off the
    // first token so the last name isn't left blank unnecessarily.
    [Fact]
    public async Task Full_Name_In_FirstName_Is_Split_Into_First_And_Last()
    {
        const string verifiedEmail = "full-name@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-full", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Jane Doe", LastName: null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u =>
            u.FirstName == "Jane" &&
            u.LastName == "Doe")), Times.Once);
    }

    // Inactive existing Apple user is rejected (mirrors the Google handler).
    [Fact]
    public async Task Inactive_Existing_User_Is_Rejected()
    {
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Apple
        });
        existing.IsActive = false;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-1", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidPassword, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
