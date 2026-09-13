using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The single grant mechanism behind both the GDPR consent endpoint and the partner-onboarding
/// checkbox. IP and device label are read server-side so the legal-audit fields cannot be spoofed by
/// the client. The document version is whatever the caller says is in force: null for a consent with
/// no document (or an employee's, ADR-0041), the dated constant for a customer's legal text
/// (ADR-0062 D4).
/// </summary>
public class ConsentServiceTests
{
    private const string UserId = "user-1";
    private const string Version = "2026-09-draft";
    private const string OlderVersion = "2025-01-old";

    private readonly Mock<IUserConsentRepository> _userConsentRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();

    private ConsentService CreateService() => new(_requestMetadata.Object, _userConsentRepository.Object);

    public ConsentServiceTests()
    {
        _requestMetadata.SetupGet(m => m.IpAddress).Returns("203.0.113.9");
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns("Chrome/Windows");
    }

    private void Existing(UserConsent consent) =>
        _userConsentRepository
            .Setup(r => r.GetByUserAndTypeAsync(UserId, consent.ConsentType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consent);

    [Fact]
    public async Task First_Grant_Adds_A_Row_And_Reports_Granted()
    {
        UserConsent? added = null;
        _userConsentRepository.Setup(r => r.Add(It.IsAny<UserConsent>())).Callback<UserConsent>(c => added = c);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.DataProcessing, null, CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(UserId, added!.UserId);
        Assert.Equal(ConsentType.DataProcessing, added.ConsentType);
        Assert.Equal("203.0.113.9", added.IpAddress);
        Assert.Equal("Chrome/Windows", added.UserAgent);
        Assert.Null(added.DocumentVersion);
    }

    [Fact]
    public async Task First_Grant_Stamps_The_Version_In_Force()
    {
        UserConsent? added = null;
        _userConsentRepository.Setup(r => r.Add(It.IsAny<UserConsent>())).Callback<UserConsent>(c => added = c);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.TermsOfService, Version, CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(Version, added!.DocumentVersion);
    }

    [Fact]
    public async Task An_Already_Granted_Consent_Reports_Not_Granted_And_Writes_Nothing()
    {
        var existing = UserConsent.Grant(UserId, ConsentType.DataProcessing, "198.51.100.1", "Firefox");
        Existing(existing);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.DataProcessing, null, CancellationToken.None);

        Assert.False(granted);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.Equal("198.51.100.1", existing.IpAddress);
    }

    [Fact]
    public async Task An_Already_Granted_Consent_Under_The_Same_Version_Is_Left_Untouched()
    {
        var existing = UserConsent.Grant(UserId, ConsentType.TermsOfService, "198.51.100.1", "Firefox", Version);
        Existing(existing);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.TermsOfService, Version, CancellationToken.None);

        Assert.False(granted);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.Equal(Version, existing.DocumentVersion);
        Assert.Equal("198.51.100.1", existing.IpAddress);
        Assert.Equal("Firefox", existing.UserAgent);
    }

    [Fact]
    public async Task An_Already_Granted_Consent_Under_An_Older_Version_Is_Re_Accepted_On_The_Same_Row()
    {
        var existing = UserConsent.Grant(UserId, ConsentType.TermsOfService, "198.51.100.1", "Firefox", OlderVersion);
        Existing(existing);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.TermsOfService, Version, CancellationToken.None);

        Assert.True(granted);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.True(existing.IsGranted);
        Assert.Equal(Version, existing.DocumentVersion);
        Assert.Equal("203.0.113.9", existing.IpAddress);
        Assert.Equal("Chrome/Windows", existing.UserAgent);
    }

    // A row granted before versioning existed reads "version unknown"; an explicit re-acceptance is
    // the one moment it may learn its version (Q-AUD-L2: no re-prompt, no backfill).
    [Fact]
    public async Task A_Legacy_Unversioned_Row_Learns_The_Version_On_An_Explicit_Re_Acceptance()
    {
        var existing = UserConsent.Grant(UserId, ConsentType.TermsOfService, "198.51.100.1", "Firefox");
        Existing(existing);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.TermsOfService, Version, CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(Version, existing.DocumentVersion);
    }

    // The employee path: no version in force for the subject, so a versioned row never moves and an
    // unversioned one is simply already granted.
    [Fact]
    public async Task No_Version_In_Force_Never_Re_Accepts_A_Granted_Row()
    {
        var existing = UserConsent.Grant(UserId, ConsentType.TermsOfService, "198.51.100.1", "Firefox", OlderVersion);
        Existing(existing);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.TermsOfService, null, CancellationToken.None);

        Assert.False(granted);
        Assert.Equal(OlderVersion, existing.DocumentVersion);
        Assert.Equal("198.51.100.1", existing.IpAddress);
    }

    [Fact]
    public async Task A_Withdrawn_Consent_Is_Regranted_On_The_Existing_Row()
    {
        var withdrawn = UserConsent
            .Grant(UserId, ConsentType.DataProcessing, "198.51.100.1", "Firefox")
            .Withdraw();
        Existing(withdrawn);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.DataProcessing, null, CancellationToken.None);

        Assert.True(granted);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.True(withdrawn.IsGranted);
        Assert.Null(withdrawn.WithdrawnAt);
        Assert.Equal("203.0.113.9", withdrawn.IpAddress);
    }

    [Fact]
    public async Task A_Withdrawn_Consent_Regranted_Under_A_Version_Carries_It()
    {
        var withdrawn = UserConsent
            .Grant(UserId, ConsentType.PrivacyPolicy, "198.51.100.1", "Firefox", OlderVersion)
            .Withdraw();
        Existing(withdrawn);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.PrivacyPolicy, Version, CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(Version, withdrawn.DocumentVersion);
    }
}
