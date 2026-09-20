using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
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
/// ADR-0062 D4 — the social commands are sign-in-or-register and are marked as the SIGN-IN only; the
/// proof of a social registration is the two versioned consent rows the PROVISIONING branch writes,
/// stamped with the documents in force for the market the sign-up named. A sign-in of an existing
/// account writes none, and a refused provisioning writes none. The session row's two branches are
/// pinned by <see cref="SocialSignInAuditEvidenceTests"/>.
/// </summary>
public sealed class SocialAuthProvisioningConsentTests
{
    private const string VerifiedEmail = "social@example.com";
    private const string Market = "country-cze";

    private readonly Mock<ITokenService> _tokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly IHostAudienceProvider _hostAudience = new HostAudienceProvider(JwtAudiences.Customer);
    private readonly LegalDocument _terms = LegalDocumentFixtures.Terms();
    private readonly LegalDocument _privacy = LegalDocumentFixtures.Privacy();
    private readonly Mock<ILegalDocumentResolver> _legalDocuments;
    private readonly AuditContext _auditContext = new();
    private User? _provisionedUser;

    public SocialAuthProvisioningConsentTests()
    {
        _tokenService
            .Setup(t => t.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
        _userRepository
            .Setup(r => r.Add(It.IsAny<User>()))
            .Callback<User>(user => _provisionedUser = user);
        _consentService
            .Setup(s => s.TryGrantAsync(It.IsAny<string>(), It.IsAny<ConsentType>(), It.IsAny<LegalDocument?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _legalDocuments = LegalDocumentFixtures.Resolver(_terms, _privacy);
    }

    private GoogleAuth.Handler GoogleHandler(GoogleVerifiedClaims? claims)
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier.Setup(v => v.VerifyAsync("token", It.IsAny<CancellationToken>())).ReturnsAsync(claims);
        return new GoogleAuth.Handler(
            verifier.Object, _tokenService.Object, _userRepository.Object, _hostAudience,
            _consentService.Object, _legalDocuments.Object, _auditContext, Mock.Of<ICompanySignInGate>());
    }

    private AppleAuth.Handler AppleHandler(AppleVerifiedClaims? claims)
    {
        var verifier = new Mock<IAppleTokenVerifier>();
        verifier.Setup(v => v.VerifyAsync("token", "nonce", It.IsAny<CancellationToken>())).ReturnsAsync(claims);
        return new AppleAuth.Handler(
            verifier.Object, _tokenService.Object, _userRepository.Object, _hostAudience,
            _consentService.Object, _legalDocuments.Object, NullLogger<AppleAuth.Handler>.Instance, _auditContext, Mock.Of<ICompanySignInGate>());
    }

    private static GoogleAuth.Command GoogleCommand(bool termsAccepted) =>
        new("token", "ignored", "ignored@x", "First", "Last", termsAccepted, CountryId: Market);

    private static AppleAuth.Command AppleCommand(bool termsAccepted) =>
        new("token", "nonce", "First", "Last", termsAccepted, CountryId: Market);

    private void AssertBothLegalConsentsGrantedFor(string userId)
    {
        _legalDocuments.Verify(r => r.ResolveInForceAsync(LegalDocumentType.TermsOfService, Market, It.IsAny<CancellationToken>()), Times.Once);
        _legalDocuments.Verify(r => r.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, Market, It.IsAny<CancellationToken>()), Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(userId, ConsentType.TermsOfService, _terms, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.Verify(
            s => s.TryGrantAsync(userId, ConsentType.PrivacyPolicy, _privacy, It.IsAny<CancellationToken>()),
            Times.Once);
        _consentService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Google_Provisioning_Grants_Both_Legal_Consents_Versioned_On_The_New_User()
    {
        var result = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        AssertBothLegalConsentsGrantedFor(_provisionedUser!.Id);
    }

    [Fact]
    public async Task Apple_Provisioning_Grants_Both_Legal_Consents_Versioned_On_The_New_User()
    {
        var result = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(_provisionedUser);
        AssertBothLegalConsentsGrantedFor(_provisionedUser!.Id);
    }

    [Fact]
    public async Task A_Google_SignIn_Of_An_Existing_Account_Grants_Nothing()
    {
        var existing = User.CreateWithGoogle(VerifiedEmail, "First", "Last", "sub-1");
        _userRepository
            .Setup(r => r.GetByGoogleIdIgnoringTenantAsync("sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _consentService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task An_Apple_SignIn_Of_An_Existing_Account_Grants_Nothing()
    {
        var existing = User.CreateWithApple(VerifiedEmail, "First", "Last", "apple-sub-1");
        _userRepository
            .Setup(r => r.GetByAppleIdIgnoringTenantAsync("apple-sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _consentService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Refused_Provisioning_Grants_Nothing()
    {
        var google = await GoogleHandler(new GoogleVerifiedClaims("sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(GoogleCommand(termsAccepted: false), CancellationToken.None);
        var apple = await AppleHandler(new AppleVerifiedClaims("apple-sub-1", VerifiedEmail, EmailVerified: true))
            .Handle(AppleCommand(termsAccepted: false), CancellationToken.None);

        Assert.True(google.IsFailure);
        Assert.True(apple.IsFailure);
        _consentService.VerifyNoOtherCalls();
    }
}
