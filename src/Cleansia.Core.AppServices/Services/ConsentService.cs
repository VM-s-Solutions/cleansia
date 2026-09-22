using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Services;

public class ConsentService(
    IRequestMetadataProvider requestMetadata,
    IUserConsentRepository userConsentRepository) : IConsentService
{
    public async Task<bool> TryGrantAsync(string userId, ConsentType consentType, LegalDocument? document, CancellationToken cancellationToken)
    {
        var existing = await userConsentRepository.GetByUserAndTypeAsync(userId, consentType, cancellationToken);

        // IP and device are read server-side, never from the request body — a client that could name
        // its own audit fields could forge the proof of consent.
        var ipAddress = requestMetadata.IpAddress;
        var userAgent = requestMetadata.DeviceLabel;

        if (existing is null)
        {
            userConsentRepository.Add(UserConsent.Grant(userId, consentType, ipAddress, userAgent, document?.Version, document?.Id));
            return true;
        }

        if (!existing.IsGranted)
        {
            existing.Regrant(ipAddress, userAgent, document?.Version, document?.Id);
            return true;
        }

        // The document's identity, not its version string: a market's own copy can be seeded under the
        // platform-wide date, and a consent must then point at the text the customer actually accepted.
        if (document is null || existing.LegalDocumentId == document.Id)
        {
            return false;
        }

        existing.AcceptVersion(document.Version, ipAddress, userAgent, document.Id);
        return true;
    }
}
