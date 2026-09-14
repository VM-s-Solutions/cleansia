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
    public async Task<LegalDocument?> ResolveInForceAsync(LegalDocumentType type, string? countryId, CancellationToken cancellationToken)
    {
        var marketCountryId = countryId
            ?? (await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken))?.CountryId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var document = await legalDocumentRepository.GetInForceAsync(
            LegalDocumentAudience.Customer, type, marketCountryId, today, cancellationToken);

        if (document is null)
        {
            logger.LogWarning(
                "No customer {Type} legal document is in force on {Today} for market {CountryId}; the consent is recorded without a version",
                type, today, marketCountryId ?? "(default)");
        }

        return document;
    }
}
