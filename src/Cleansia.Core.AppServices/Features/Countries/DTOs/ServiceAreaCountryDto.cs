using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Countries.DTOs;

public record ServiceAreaCountryDto(
    string Id,
    string IsoCode,
    string IsoAlpha2,
    string Name,
    Dictionary<string, Translation> Translations,
    bool IsServiced,
    bool IsDefaultMarket,
    bool HasConfiguration);
