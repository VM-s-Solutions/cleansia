using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record AdminPackageDetailDto(
    string Id,
    string Name,
    string Description,
    string? Tagline,
    bool IsPopular,
    Dictionary<string, decimal> Prices,
    Dictionary<string, Translation> Translations,
    IEnumerable<PackageServiceDto> IncludedServices,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);

public record PackageServiceDto(
    string Id,
    string Name,
    string Description,
    decimal PriceWeight);