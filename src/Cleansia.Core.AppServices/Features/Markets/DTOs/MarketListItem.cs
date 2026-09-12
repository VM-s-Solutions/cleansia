using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Markets.DTOs;

/// <summary>
/// One market a customer may browse in: a serviced country joined to its configured currency, plus
/// the per-market figures customer copy interpolates (ADR-0058 D1, ADR-0060). <c>IsoCode</c> is what
/// a client persists; <c>IsoAlpha2</c> is what the chip prints beside <c>CurrencyCode</c>.
/// </summary>
public record MarketListItem(
    string CountryId,
    string IsoCode,
    string IsoAlpha2,
    string Name,
    Dictionary<string, Translation> Translations,
    string CurrencyId,
    string CurrencyCode,
    string CurrencySymbol,
    bool IsDefault,
    decimal? NoShowCredit,
    decimal? InsuranceCoverageAmount);
