using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The single grant mechanism behind both the GDPR consent endpoint and the partner-onboarding
/// checkbox. IP and device label are read server-side so the legal-audit fields cannot be spoofed by
/// the client.
/// </summary>
public class ConsentServiceTests
{
    private const string UserId = "user-1";

    private readonly Mock<IUserConsentRepository> _userConsentRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();

    private ConsentService CreateService() => new(_requestMetadata.Object, _userConsentRepository.Object);

    public ConsentServiceTests()
    {
        _requestMetadata.SetupGet(m => m.IpAddress).Returns("203.0.113.9");
        _requestMetadata.SetupGet(m => m.DeviceLabel).Returns("Chrome/Windows");
    }

    [Fact]
    public async Task First_Grant_Adds_A_Row_And_Reports_Granted()
    {
        UserConsent? added = null;
        _userConsentRepository.Setup(r => r.Add(It.IsAny<UserConsent>())).Callback<UserConsent>(c => added = c);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.DataProcessing, CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(UserId, added!.UserId);
        Assert.Equal(ConsentType.DataProcessing, added.ConsentType);
        Assert.Equal("203.0.113.9", added.IpAddress);
        Assert.Equal("Chrome/Windows", added.UserAgent);
    }

    [Fact]
    public async Task An_Already_Granted_Consent_Reports_Not_Granted_And_Writes_Nothing()
    {
        var existing = UserConsent.Grant(UserId, ConsentType.DataProcessing, "198.51.100.1", "Firefox");
        _userConsentRepository
            .Setup(r => r.GetByUserAndTypeAsync(UserId, ConsentType.DataProcessing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.DataProcessing, CancellationToken.None);

        Assert.False(granted);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.Equal("198.51.100.1", existing.IpAddress);
    }

    [Fact]
    public async Task A_Withdrawn_Consent_Is_Regranted_On_The_Existing_Row()
    {
        var withdrawn = UserConsent
            .Grant(UserId, ConsentType.DataProcessing, "198.51.100.1", "Firefox")
            .Withdraw();
        _userConsentRepository
            .Setup(r => r.GetByUserAndTypeAsync(UserId, ConsentType.DataProcessing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(withdrawn);

        var granted = await CreateService().TryGrantAsync(UserId, ConsentType.DataProcessing, CancellationToken.None);

        Assert.True(granted);
        _userConsentRepository.Verify(r => r.Add(It.IsAny<UserConsent>()), Times.Never);
        Assert.True(withdrawn.IsGranted);
        Assert.Null(withdrawn.WithdrawnAt);
        Assert.Equal("203.0.113.9", withdrawn.IpAddress);
    }
}
