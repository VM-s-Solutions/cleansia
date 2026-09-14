using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Tests.Domain.Legal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// A social command is marked <c>customer.session.login</c>. The sign-in of an existing account records
/// the method and the audience keyed on that account (the session has no user yet, so the handler names
/// the actor); the provisioning branch declines the row, because the account did not exist when the act
/// began and its proof is the consent rows — and the pipeline, not the handler, decides what a refusal
/// records, so a refused provisioning neither records evidence nor declines anything.
/// </summary>
public sealed class SocialSignInAuditEvidenceTests
{
    private const string VerifiedEmail = "social@example.com";

    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly IHostAudienceProvider _hostAudience = new HostAudienceProvider(JwtAudiences.Customer);
    private readonly AuditContext _auditContext = new();

    public SocialSignInAuditEvidenceTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
        _consentService
            .Setup(s => s.TryGrantAsync(It.IsAny<string>(), It.IsAny<ConsentType>(), It.IsAny<LegalDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private GoogleAuth.Handler GoogleHandler(GoogleVerifiedClaims? claims)
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier.Setup(v => v.VerifyAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(claims);
        return new GoogleAuth.Handler(
            verifier.Object, _tokenService.Object, _cartRepository.Object, _userRepository.Object, _hostAudience,
            _consentService.Object, LegalDocumentFixtures.Resolver().Object, _auditContext);
    }

    private AppleAuth.Handler AppleHandler(AppleVerifiedClaims? claims)
    {
        var verifier = new Mock<IAppleTokenVerifier>();
        verifier.Setup(v => v.VerifyAsync("token", "nonce", It.IsAny<CancellationToken>())).ReturnsAsync(claims);
        return new AppleAuth.Handler(
            verifier.Object, _tokenService.Object, _cartRepository.Object, _userRepository.Object, _hostAudience,
            _consentService.Object, LegalDocumentFixtures.Resolver().Object, NullLogger<AppleAuth.Handler>.Instance, _auditContext);
    }

    private static GoogleAuth.Command GoogleCommand(bool termsAccepted) =>
        new("token", "ignored", "ignored@x", "First", "Last", termsAccepted);

    private static AppleAuth.Command AppleCommand(bool termsAccepted) =>
        new("token", "nonce", "First", "Last", termsAccepted);

    private JsonElement Payload() => JsonDocument.Parse(_auditContext.DrainSnapshot()!.AfterJson!).RootElement;

    [Fact]
    public async Task A_Google_SignIn_Of_An_Existing_Account_Records_The_Google_Method_Keyed_On_That_Account()
    {
        var existing = User.CreateWithGoogle(VerifiedEmail, "First", "Last", "sub-1");
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync("sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(_auditContext.SuccessRowDeclined);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(existing.Id, snapshot.ResourceId);
        Assert.Equal(existing.Id, snapshot.ActorUserId);
        var payload = JsonDocument.Parse(snapshot.AfterJson!).RootElement;
        Assert.Equal(LoginEvidence.GoogleMethod, payload.GetProperty("method").GetString());
        Assert.True(payload.GetProperty("rememberMe").GetBoolean());
        Assert.Equal(JwtAudiences.Customer, payload.GetProperty("clientAudience").GetString());
        Assert.True(payload.GetProperty("emailConfirmed").GetBoolean());
        Assert.DoesNotContain(VerifiedEmail, snapshot.AfterJson);
    }

    [Fact]
    public async Task An_Apple_SignIn_Of_An_Existing_Account_Records_The_Apple_Method_Keyed_On_That_Account()
    {
        var existing = User.CreateWithApple(VerifiedEmail, "First", "Last", "apple-sub-1");
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(_auditContext.SuccessRowDeclined);
        Assert.Equal(LoginEvidence.AppleMethod, Payload().GetProperty("method").GetString());
    }

    [Fact]
    public async Task A_Google_Provisioning_Declines_The_Session_Row_And_Records_No_Evidence()
    {
        var result = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_auditContext.SuccessRowDeclined);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Fact]
    public async Task An_Apple_Provisioning_Declines_The_Session_Row_And_Records_No_Evidence()
    {
        var result = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_auditContext.SuccessRowDeclined);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Fact]
    public async Task A_Refused_SignIn_Declines_Nothing_So_The_Pipeline_Records_The_Refusal()
    {
        var badToken = await GoogleHandler(claims: null).Handle(GoogleCommand(termsAccepted: false), CancellationToken.None);
        var noAccount = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(badToken.IsFailure);
        Assert.True(noAccount.IsFailure);
        Assert.False(_auditContext.SuccessRowDeclined);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    // ── a refusal on a KNOWN account names it ──────────────────────────────────

    /// <summary>
    /// The account-type guard refuses a social token onto an existing password account; that refusal is
    /// the account's row, so the handler names the subject it resolved — id only, no payload — before
    /// refusing. The failure arm of the pipeline reads it; the caller is told nothing more than before.
    /// </summary>
    [Fact]
    public async Task A_Google_Token_Refused_Onto_A_Password_Account_Names_That_Account_With_No_Payload()
    {
        var existing = User.CreateWithPassword(VerifiedEmail, "Passw0rd!", "First", "Last");
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(VerifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InternalAuthTypeError, result.Error!.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal(existing.Id, snapshot!.ActorUserId);
        Assert.Equal("User", snapshot.ResourceType);
        Assert.Equal(existing.Id, snapshot.ResourceId);
        Assert.Null(snapshot.AfterJson);
    }

    [Fact]
    public async Task An_Apple_Token_Refused_Onto_A_Google_Account_Names_That_Account_With_No_Payload()
    {
        var existing = User.CreateWithGoogle(VerifiedEmail, "First", "Last", "sub-1");
        _userRepository
            .Setup(r => r.GetByEmailIgnoringTenantAsync(VerifiedEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.GoogleAuthTypeError, result.Error!.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.Equal(existing.Id, snapshot?.ActorUserId);
        Assert.Null(snapshot?.AfterJson);
    }

    [Fact]
    public async Task A_Deactivated_Account_Refused_A_Google_SignIn_Is_Named()
    {
        var existing = User.CreateWithGoogle(VerifiedEmail, "First", "Last", "sub-1");
        existing.IsActive = false;
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync("sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(existing.Id, _auditContext.DrainSnapshot()?.ActorUserId);
    }
}
