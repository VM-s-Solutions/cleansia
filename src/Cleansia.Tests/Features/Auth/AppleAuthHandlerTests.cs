using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging;
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
///     (covers BOTH Internal AND Google) is rejected — closing the verified-email-collision takeover for
///     Apple exactly as Google's hardening did — and the rejection message names the provider that
///     account ACTUALLY uses, so a Google user is not told to use a password they never set;
///   - provisioning happens ONLY when <c>claims.EmailVerified</c>, and so does the EMAIL FALLBACK that
///     resolves a pre-sub-storage account — matching one binds the token's sub to it permanently, so an
///     unverified address must reach neither the account nor its identity anchor. A SUB match is
///     deliberately not gated: that sub was bound while the email was verified;
///   - a RETURNING user resolves by the verified Apple <c>sub</c> even when Apple omits the email claim
///     (Apple guarantees the email only on the FIRST authorization), and the sub-matched account keeps
///     its stored email; the collision and IsActive guards still run on that path;
///   - provisioning NEVER persists a blank name part (an empty name fails UpdateCurrentUser's NotEmpty
///     rules and CreateOrder's 2-character CustomerName floor, i.e. it blocks booking on an account Apple
///     can never re-send a name for), and a returning account's blank OR system-generated name is
///     back-filled from a genuinely supplied one — but a name the user typed is never overwritten.
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
    private readonly List<(LogLevel Level, string Message)> _logEntries = [];
    private User? _provisionedUser;

    public AppleAuthHandlerTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
        _userRepository
            .Setup(r => r.Add(It.IsAny<User>()))
            .Callback<User>(user => _provisionedUser = user);
    }

    private AppleAuth.Handler CreateHandler() =>
        (AppleAuth.Handler)Activator.CreateInstance(
            typeof(AppleAuth.Handler),
            _verifier.Object,
            _tokenService.Object,
            _cartRepository.Object,
            _userRepository.Object,
            _hostAudience,
            new CapturingLogger<AppleAuth.Handler>(_logEntries))!;

    // Defaults to the signup screen's shape so the provisioning branch stays reachable; the sign-in
    // screen sends no tick and its tests pass termsAccepted: false explicitly.
    private static AppleAuth.Command Command(bool termsAccepted = true) =>
        new(IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "First", LastName: "Last",
            TermsAccepted: termsAccepted);

    private static User BlankNameAppleUser(string firstName, string lastName, string? email = null)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Apple,
            FirstName = firstName,
            LastName = lastName,
            Email = email
        });
        user.IsActive = true;

        return user;
    }

    // The two rules a stored name has to satisfy for the customer to be able to book:
    // UpdateCurrentUser validates each part NotEmpty, CreateOrder needs a 2+ character CustomerName.
    private static void AssertUsableName(string namePart)
    {
        Assert.False(string.IsNullOrWhiteSpace(namePart));
        Assert.True(namePart.Length >= 2, $"'{namePart}' is shorter than the 2-character minimum.");
        Assert.True(namePart.All(char.IsLetter), $"'{namePart}' is not name-shaped.");
    }

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
    // Two independent properties, and only the second one is new: the account is NEVER bound (no token,
    // no user, no cart — the S1 property, unchanged), and the message names the provider that account
    // ACTUALLY uses instead of always saying "use your email and password".
    [Theory]
    [InlineData(AuthenticationType.Internal, BusinessErrorMessage.InternalAuthTypeError)]
    [InlineData(AuthenticationType.Google, BusinessErrorMessage.GoogleAuthTypeError)]
    public async Task Existing_NonApple_Account_With_Verified_Email_Is_Rejected_Naming_Its_Provider(
        AuthenticationType collidingType, string expectedMessage)
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
        Assert.Equal(expectedMessage, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
    }

    // Stricter than Google today: a verified token whose email_verified is false provisions NOTHING —
    // and says so in the log, because a silent rejection here is indistinguishable from a bad signature.
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

        var warning = Assert.Single(_logEntries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain("unverified@example.com", warning.Message);
    }

    // The returning-user gap: Apple guarantees the email claim only on the FIRST authorization, so a
    // later sign-in arrives with a sub and NO email. The account must resolve by the verified sub — the
    // email lookup is unreachable and must not be attempted.
    [Fact]
    public async Task Returning_User_Without_Email_Claim_Is_Resolved_By_AppleId()
    {
        const string appleSub = "apple-sub-returning";
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Apple
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(appleSub, Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(appleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The sub is the stable identity, so it wins over the email — and the matched account KEEPS its
    // stored email (rewriting it would collide with the (TenantId, Email) unique index and silently
    // merge accounts when the user switches between a relay address and their real one).
    [Fact]
    public async Task AppleId_Match_Wins_Over_Email_And_Does_Not_Rewrite_The_Stored_Email()
    {
        const string appleSub = "apple-sub-stable";
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Apple
        });
        existing.IsActive = true;
        var storedEmail = existing.Email;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(appleSub, "different-relay@privaterelay.appleid.com", EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(appleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(storedEmail, existing.Email);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }

    // S1: the account-type collision guard runs on whichever account was resolved — including the
    // AppleId path — so an Apple login can never bind into an Internal/Google account. The message
    // names the resolved account's real provider on this path too.
    [Theory]
    [InlineData(AuthenticationType.Internal, BusinessErrorMessage.InternalAuthTypeError)]
    [InlineData(AuthenticationType.Google, BusinessErrorMessage.GoogleAuthTypeError)]
    public async Task Account_Found_By_AppleId_With_NonApple_AuthenticationType_Is_Rejected(
        AuthenticationType collidingType, string expectedMessage)
    {
        const string appleSub = "apple-sub-collide";
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = collidingType
        });
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(appleSub, Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(appleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedMessage, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // An account matched by sub is still subject to the IsActive gate.
    [Fact]
    public async Task Inactive_Account_Found_By_AppleId_Is_Rejected()
    {
        const string appleSub = "apple-sub-inactive";
        var existing = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            AuthenticationType = AuthenticationType.Apple
        });
        existing.IsActive = false;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims(appleSub, Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(appleSub, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidPassword, result.Error!.Message);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The email claim is optional for a RETURNING user only. With no matching sub there is no account
    // to sign into and no verified email to build one around, so provisioning must be refused.
    [Fact]
    public async Task Unknown_AppleId_Without_Email_Claim_Cannot_Provision()
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-unknown", Email: null, EmailVerified: false));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidAppleUserToken, result.Error!.Message);
        Assert.Equal(nameof(AppleAuth.Command.IdentityToken), result.Error!.Code);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Single(_logEntries, e => e.Level == LogLevel.Warning);
    }

    // Regression (the reported bug): Apple omits the family name on the first authorization, so the
    // command arrives with a null LastName. Provisioning must succeed AND must not store a blank last
    // name — an empty part fails UpdateCurrentUser's NotEmpty rule and leaves the customer unable to book.
    // The supplied given name is kept; only the blank part is filled from the verified email.
    [Fact]
    public async Task Missing_LastName_Is_Filled_From_The_Verified_Email_LocalPart()
    {
        const string verifiedEmail = "jane.doe@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-nolast", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Janet", LastName: null, TermsAccepted: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        Assert.Equal(verifiedEmail, _provisionedUser!.Email);
        Assert.Equal("Janet", _provisionedUser.FirstName);
        Assert.Equal("Doe", _provisionedUser.LastName);
        Assert.Equal(AuthenticationType.Apple, _provisionedUser.AuthenticationType);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
    }

    // A later Apple sign-in that provisions (e.g. the account was deleted and recreated) carries no name
    // at all — both fields null. Provisioning must still succeed, with BOTH parts derived from the
    // verified email's local part rather than persisted blank.
    [Fact]
    public async Task Missing_Both_Names_Are_Derived_From_The_Verified_Email_LocalPart()
    {
        const string verifiedEmail = "jane.doe2024@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-noname", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: null, LastName: null, TermsAccepted: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        Assert.Equal(verifiedEmail, _provisionedUser!.Email);
        Assert.Equal("Jane", _provisionedUser.FirstName);
        Assert.Equal("Doe", _provisionedUser.LastName);
    }

    // A local part with nothing name-shaped in it (all digits, or single letters) must not be persisted as
    // a name — the neutral placeholder takes over. Whatever it is, it has to satisfy the two rules the
    // customer is blocked by: NotEmpty (UpdateCurrentUser) and 2+ characters (CreateOrder.CustomerName).
    [Theory]
    [InlineData("12345678@example.com")]
    [InlineData("j.d@example.com")]
    [InlineData("_@example.com")]
    public async Task Unusable_Email_LocalPart_Falls_Back_To_A_Usable_Neutral_Name(string verifiedEmail)
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-unusable", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: null, LastName: null, TermsAccepted: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        AssertUsableName(_provisionedUser!.FirstName);
        AssertUsableName(_provisionedUser.LastName);
    }

    // An Apple private-relay address is a random token, never name-shaped, so it must not be mined for a
    // name — that would persist gibberish the user then has to notice and correct.
    [Fact]
    public async Task PrivateRelay_Email_Is_Not_Mined_For_A_Name()
    {
        const string relayLocalPart = "k9x7m2q4r5";
        const string verifiedEmail = $"{relayLocalPart}@privaterelay.appleid.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-relay", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: null, LastName: null, TermsAccepted: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        AssertUsableName(_provisionedUser!.FirstName);
        AssertUsableName(_provisionedUser.LastName);

        var relayLetters = new string(relayLocalPart.Where(char.IsLetter).ToArray());
        Assert.DoesNotContain(relayLetters, _provisionedUser.FirstName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(relayLetters, _provisionedUser.LastName, StringComparison.OrdinalIgnoreCase);
    }

    // The email fallback is a floor, not a rewrite: a name Apple actually handed us always wins.
    [Fact]
    public async Task Supplied_Name_Wins_Over_The_Email_Fallback_At_Provisioning()
    {
        const string verifiedEmail = "someone.else@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-supplied", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(verifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Jane", LastName: "Doe", TermsAccepted: true);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        Assert.Equal("Jane", _provisionedUser!.FirstName);
        Assert.Equal("Doe", _provisionedUser.LastName);
    }

    // Back-fill: accounts provisioned before the blank-name fix can only be repaired when Apple replays
    // the name (the user revoked the app and re-authorized). A blank stored part takes the supplied value.
    [Fact]
    public async Task Returning_User_With_Blank_Stored_Name_Is_BackFilled_From_The_Supplied_Name()
    {
        var existing = BlankNameAppleUser(firstName: string.Empty, lastName: string.Empty);
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-blank", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-blank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("First", existing.FirstName);
        Assert.Equal("Last", existing.LastName);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
    }

    // Only the blank part moves: the payload can carry an Apple-ID name the user has since replaced in the
    // profile screen, and a sign-in must never silently undo that edit.
    [Fact]
    public async Task Returning_User_BackFill_Fills_Only_The_Blank_Part()
    {
        var existing = BlankNameAppleUser(firstName: "Renamed", lastName: string.Empty);
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-partial", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-partial", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", existing.FirstName);
        Assert.Equal("Last", existing.LastName);
    }

    // Neither stored part matches what the account's email derives to, so both are user-authored and the
    // supplied payload must lose to them.
    [Fact]
    public async Task Returning_User_BackFill_Never_Overwrites_A_User_Set_Name()
    {
        var existing = BlankNameAppleUser(firstName: "Renamed", lastName: "Surname");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-named", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-named", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", existing.FirstName);
        Assert.Equal("Surname", existing.LastName);
    }

    // The back-fill is driven ONLY by a name the client actually supplied. A routine returning sign-in
    // carries none, and inventing one from the stored (possibly relay) email would be a write with no new
    // information behind it — such accounts are healed by the profile screen instead.
    [Fact]
    public async Task Returning_User_With_A_Blank_Name_Is_Healed_From_Their_Stored_Email()
    {
        var existing = BlankNameAppleUser(
            firstName: string.Empty, lastName: string.Empty, email: "jane.doe@example.com");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-nameless", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-nameless", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: null, LastName: null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        // Apple sends no name on a repeat authorization, so healing MUST come from the verified email —
        // otherwise an account provisioned blank (while this flow was broken) can never book again.
        Assert.True(result.IsSuccess);
        AssertUsableName(existing.FirstName);
        AssertUsableName(existing.LastName);
        Assert.Equal("Jane", existing.FirstName);
        Assert.Equal("Doe", existing.LastName);
    }

    // The one genuine name Apple ever sends (first authorization, e.g. after the user revoked the app in
    // Settings and re-authorized) must be able to displace a name WE generated. "cmisa695@gmail.com"
    // derives to Cmisa/Customer, so an account showing that is provably system-generated, never user typed.
    [Fact]
    public async Task Returning_User_System_Generated_Name_Is_Replaced_By_The_Supplied_Name()
    {
        var existing = BlankNameAppleUser(
            firstName: "Cmisa", lastName: "Customer", email: "cmisa695@gmail.com");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-derived", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-derived", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Michael", LastName: "Chaban");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Michael", existing.FirstName);
        Assert.Equal("Chaban", existing.LastName);
    }

    // Per-part, not all-or-nothing: the placeholder family name goes, the given name the user typed stays.
    [Fact]
    public async Task Returning_User_Placeholder_LastName_Is_Replaced_While_A_User_Set_FirstName_Survives()
    {
        var existing = BlankNameAppleUser(
            firstName: "Miguel", lastName: "Customer", email: "cmisa695@gmail.com");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-placeholder", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-placeholder", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Michael", LastName: "Chaban");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Miguel", existing.FirstName);
        Assert.Equal("Chaban", existing.LastName);
    }

    // A name the user set themselves is never touched, however genuine the Apple payload is.
    [Fact]
    public async Task Returning_User_UserEdited_Name_Is_Not_Replaced_By_The_Supplied_Name()
    {
        var existing = BlankNameAppleUser(
            firstName: "Miguel", lastName: "Chabanov", email: "cmisa695@gmail.com");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-edited", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-edited", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Michael", LastName: "Chaban");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Miguel", existing.FirstName);
        Assert.Equal("Chabanov", existing.LastName);
    }

    // A routine returning sign-in carries no name, so there is nothing genuine to promote — the
    // system-generated name stays exactly as stored rather than being rewritten for no reason.
    [Fact]
    public async Task Returning_User_Without_A_Supplied_Name_Keeps_The_System_Generated_Name()
    {
        var existing = BlankNameAppleUser(
            firstName: "Cmisa", lastName: "Customer", email: "cmisa695@gmail.com");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("apple-sub-nopayload", Email: null, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-nopayload", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var command = new AppleAuth.Command(
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: null, LastName: null);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Cmisa", existing.FirstName);
        Assert.Equal("Customer", existing.LastName);
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
            IdentityToken: "any-token", RawNonce: "any-raw-nonce", FirstName: "Jane Doe", LastName: null, TermsAccepted: true);

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

    // Apple's consent sheet lets the user clear EITHER name field, and the one-shot payload arrives only
    // once — so a full name folded into whichever single field survived must be split rather than half
    // discarded and back-filled with a placeholder (which then reads as user-authored forever). The
    // family-name side splits at the LAST space so multi-word surnames stay whole. A first/last swap is
    // possible and accepted: the user can fix it in Edit profile; a placeholder is far harder to displace.
    [Theory]
    [InlineData("Michael Chaban", null, "Michael", "Chaban")]
    [InlineData(null, "Michael Chaban", "Michael", "Chaban")]
    [InlineData("", "Anna van der Berg", "Anna van der", "Berg")]
    [InlineData("Michael", "Chaban", "Michael", "Chaban")]
    public async Task A_Full_Name_In_Either_Single_Field_Is_Split_Across_Both(
        string? suppliedFirst, string? suppliedLast, string expectedFirst, string expectedLast)
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-split", "cmisa695@example.com", EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(
            new AppleAuth.Command(
                IdentityToken: "any-token",
                RawNonce: "any-raw-nonce",
                FirstName: suppliedFirst,
                LastName: suppliedLast,
                TermsAccepted: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        Assert.Equal(expectedFirst, _provisionedUser!.FirstName);
        Assert.Equal(expectedLast, _provisionedUser.LastName);
    }

    // An Apple account whose row predates sub-storage is findable ONLY by the email fallback, and Apple
    // sends the email ONLY on the first authorization — so unless the sub is bound on the one sign-in that
    // still carries an email, every later sign-in misses on the sub, has no email to fall back to, and the
    // user is locked out of their own account with no self-service recovery.
    [Fact]
    public async Task Email_Matched_Account_Without_A_Sub_Gets_The_Verified_Subject_Bound()
    {
        var existing = BlankNameAppleUser("Jane", "Doe", "jane.doe@example.com");
        Assert.Null(existing.AppleId); // precondition: the pre-sub-storage row shape
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-newly-seen", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("sub-newly-seen", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("sub-newly-seen", existing.AppleId);
    }

    // The never-overwrite half is a SECURITY property (S1): an already-bound sub is the account's verified
    // identity anchor, so a later sign-in must never be able to re-point it at a different Apple ID.
    [Fact]
    public async Task An_Already_Bound_Subject_Is_Never_Rewritten()
    {
        var existing = User.CreateWithApple("jane.doe@example.com", "Jane", "Doe", "sub-original");
        existing.IsActive = true;
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-different", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("sub-different", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.Equal("sub-original", existing.AppleId);
    }

    // Matching by email does not just sign the caller in, it PERMANENTLY binds the token's sub to that
    // row — an anchor no later sign-in can rewrite. So the fallback must run on an email APPLE vouched
    // for: a token asserting an unverified address must reach neither the account nor its anchor.
    [Fact]
    public async Task Unverified_Email_Does_Not_Match_An_Existing_Account_Or_Bind_Its_Subject()
    {
        var victim = BlankNameAppleUser("Jane", "Doe", "jane.doe@example.com");
        Assert.Null(victim.AppleId); // precondition: the pre-sub-storage row shape, reachable by email only
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("attacker-sub", victim.Email, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("attacker-sub", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(victim.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(victim);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidAppleUserToken, result.Error!.Message);
        Assert.Null(victim.AppleId);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
    }

    // The other half of that gate: it narrows the fallback, it does not remove it. The first sign-in that
    // still carries a VERIFIED email is the one chance a pre-sub-storage row has to be found and anchored.
    [Fact]
    public async Task Verified_Email_Still_Matches_An_Existing_Account()
    {
        var existing = BlankNameAppleUser("Jane", "Doe", "jane.doe@example.com");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-verified", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("sub-verified", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("sub-verified", existing.AppleId);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(existing.Email, It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The gate belongs to the EMAIL fallback alone. A sub match carries its own proof — that sub was bound
    // while the email WAS verified — and Apple stops sending email_verified meaningfully once it stops
    // sending the email, so re-gating this path would lock out every returning Apple user.
    [Fact]
    public async Task Subject_Matched_Account_Signs_In_Regardless_Of_The_EmailVerified_Claim()
    {
        var existing = BlankNameAppleUser("Jane", "Doe", "jane.doe@example.com");
        existing.LinkAppleId("sub-returning");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-returning", existing.Email, EmailVerified: false));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("sub-returning", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The sign-in screen carries no terms tick, so it sends none — and a returning user must not be asked
    // to re-accept the terms they accepted when the account was created.
    [Fact]
    public async Task Existing_Account_Signs_In_Without_The_Signup_Tick()
    {
        var existing = BlankNameAppleUser("Jane", "Doe", "jane.doe@example.com");
        existing.LinkAppleId("sub-returning");
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-returning", existing.Email, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("sub-returning", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(Command(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(existing, true, HostAudience, It.IsAny<CancellationToken>()), Times.Once);
    }

    // The hole this closes: a brand-new visitor tapping the sign-in screen's Apple button used to get an
    // account provisioned around them with no consent record at all.
    [Fact]
    public async Task Unknown_Identity_Without_The_Signup_Tick_Is_Refused_And_Creates_Nothing()
    {
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-brand-new", "brand-new@example.com", EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(Command(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.SocialAccountNotFound, result.Error!.Message);
        Assert.Equal(nameof(AppleAuth.Command.TermsAccepted), result.Error!.Code);
        _userRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Never);
        _tokenService.Verify(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The other half: the signup screen gates its Apple button on the tick and sends it, so that path
    // still brings an account into existence.
    [Fact]
    public async Task Unknown_Identity_With_The_Signup_Tick_Still_Provisions()
    {
        const string verifiedEmail = "signing-up@example.com";
        _verifier
            .Setup(v => v.VerifyAsync("any-token", "any-raw-nonce", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppleVerifiedClaims("sub-signup", verifiedEmail, EmailVerified: true));
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var result = await CreateHandler().Handle(Command(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _userRepository.Verify(r => r.Add(It.Is<User>(u => u.Email == verifiedEmail)), Times.Once);
        _cartRepository.Verify(r => r.Add(It.IsAny<Cart>()), Times.Once);
    }

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
