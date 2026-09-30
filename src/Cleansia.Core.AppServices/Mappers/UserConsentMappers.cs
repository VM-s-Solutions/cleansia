using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Mappers;

public static class UserConsentMappers
{
    public static UserConsentDto MapToDto(this UserConsent consent, LegalDocument? inForce)
    {
        return new UserConsentDto(
            consent.Id,
            consent.ConsentType,
            consent.IsGranted,
            consent.GrantedAt,
            consent.WithdrawnAt,
            consent.CreatedOn,
            consent.DocumentVersion,
            consent.Covers(inForce));
    }

    /// <summary>Each row against the text of its type in force in the default market, one resolution per type.</summary>
    public static async Task<List<UserConsentDto>> MapToDtosAsync(
        this IReadOnlyCollection<UserConsent> consents,
        ILegalDocumentResolver legalDocumentResolver,
        CancellationToken cancellationToken)
    {
        var inForce = new Dictionary<LegalDocumentType, LegalDocument?>();
        foreach (var documentType in consents.Select(c => LegalDocument.TypeFor(c.ConsentType)).OfType<LegalDocumentType>().Distinct())
        {
            inForce[documentType] = LegalDocument.CleanerConsentTypeFor(documentType) is null
                ? await legalDocumentResolver.ResolveInForceAsync(documentType, countryId: null, cancellationToken)
                : await legalDocumentResolver.ResolveInForceAsync(
                    LegalDocumentAudience.Employee, documentType, countryId: null, cancellationToken);
        }

        return consents
            .Select(c => c.MapToDto(LegalDocument.TypeFor(c.ConsentType) is { } type ? inForce[type] : null))
            .ToList();
    }
}
