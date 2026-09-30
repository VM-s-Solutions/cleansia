using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// The customer's terms and privacy acceptance at a new booking — a one-off order or a new recurring
/// schedule (owner ruling 2026-09-28): the tick is asked again whenever either consent is not an
/// acceptance of the text in force for the booking's market, and a booking made with it moves both
/// consent rows to those texts. Bookings already made keep the versions they were made under.
/// </summary>
public static class CustomerLegalConsents
{
    /// <summary>Whether the account's terms AND privacy consents are acceptances of the texts in force for the market.</summary>
    public static async Task<bool> CoverTextsInForceAsync(
        IUserConsentRepository userConsentRepository,
        ILegalDocumentResolver legalDocumentResolver,
        string userId,
        string? countryId,
        CancellationToken cancellationToken)
    {
        var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(userId, cancellationToken);

        return await CoversAsync(ConsentType.TermsOfService, LegalDocumentType.TermsOfService)
            && await CoversAsync(ConsentType.PrivacyPolicy, LegalDocumentType.PrivacyPolicy);

        async Task<bool> CoversAsync(ConsentType consentType, LegalDocumentType documentType)
        {
            var consent = consents.FirstOrDefault(c => c.ConsentType == consentType);
            return consent is not null && consent.Covers(
                await legalDocumentResolver.ResolveInForceAsync(documentType, countryId, cancellationToken));
        }
    }

    /// <summary>
    /// The versions the booking is made under. With the tick, the texts in force for the market — and a
    /// signed-in customer's two consent rows move to them, with the IP and device. Without it, the
    /// versions the customer's rows hold, which the validator proved are the ones in force. A guest
    /// without the tick has none.
    /// </summary>
    public static async Task<(string? TermsVersion, string? PrivacyVersion)> RecordAsync(
        IConsentService consentService,
        IUserConsentRepository userConsentRepository,
        ILegalDocumentResolver legalDocumentResolver,
        string? userId,
        bool? termsAccepted,
        string? countryId,
        CancellationToken cancellationToken)
    {
        if (termsAccepted == true)
        {
            var terms = await legalDocumentResolver.ResolveInForceAsync(LegalDocumentType.TermsOfService, countryId, cancellationToken);
            var privacy = await legalDocumentResolver.ResolveInForceAsync(LegalDocumentType.PrivacyPolicy, countryId, cancellationToken);
            if (!string.IsNullOrEmpty(userId))
            {
                await consentService.TryGrantAsync(userId, ConsentType.TermsOfService, terms, cancellationToken);
                await consentService.TryGrantAsync(userId, ConsentType.PrivacyPolicy, privacy, cancellationToken);
            }

            return (terms?.Version, privacy?.Version);
        }

        if (string.IsNullOrEmpty(userId))
        {
            return (null, null);
        }

        var consents = await userConsentRepository.GetByUserIdNoTrackingAsync(userId, cancellationToken);
        return (
            consents.FirstOrDefault(c => c.ConsentType == ConsentType.TermsOfService)?.DocumentVersion,
            consents.FirstOrDefault(c => c.ConsentType == ConsentType.PrivacyPolicy)?.DocumentVersion);
    }
}
