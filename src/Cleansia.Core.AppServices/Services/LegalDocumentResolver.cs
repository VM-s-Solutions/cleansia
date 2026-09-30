using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public sealed class LegalDocumentResolver(
    ILegalDocumentRepository legalDocumentRepository,
    ICountryConfigurationRepository countryConfigurationRepository,
    ILogger<LegalDocumentResolver> logger) : ILegalDocumentResolver
{
    public Task<LegalDocument?> ResolveInForceAsync(LegalDocumentType type, string? countryId, CancellationToken cancellationToken) =>
        ResolveInForceAsync(LegalDocumentAudience.Customer, type, countryId, cancellationToken);

    public async Task<LegalDocument?> ResolveInForceAsync(
        LegalDocumentAudience audience, LegalDocumentType type, string? countryId, CancellationToken cancellationToken)
    {
        var marketCountryId = countryId
            ?? (await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken))?.CountryId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var document = await legalDocumentRepository.GetInForceAsync(
            audience, type, marketCountryId, today, cancellationToken);

        // A cleaner text not yet seeded is the expected state until the texts are delivered, and every
        // take asks; only a missing customer text is worth a warning.
        if (document is null && audience == LegalDocumentAudience.Customer)
        {
            logger.LogWarning(
                "No customer {Type} legal document is in force on {Today} for market {CountryId}",
                type, today, marketCountryId ?? "(default)");
        }

        return document;
    }
}
