using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services;

public class ConsentService(
    IRequestMetadataProvider requestMetadata,
    IUserConsentRepository userConsentRepository) : IConsentService
{
    public async Task<bool> TryGrantAsync(string userId, ConsentType consentType, CancellationToken cancellationToken)
    {
        var existing = await userConsentRepository.GetByUserAndTypeAsync(userId, consentType, cancellationToken);

        // IP and device are read server-side, never from the request body — a client that could name
        // its own audit fields could forge the proof of consent.
        var ipAddress = requestMetadata.IpAddress;
        var userAgent = requestMetadata.DeviceLabel;

        if (existing is null)
        {
            userConsentRepository.Add(UserConsent.Grant(userId, consentType, ipAddress, userAgent));
            return true;
        }

        if (existing.IsGranted)
        {
            return false;
        }

        existing.Regrant(ipAddress, userAgent);
        return true;
    }
}
