using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record PackageDetails(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int EstimatedTime,
    string CurrencyCode,
    IEnumerable<string> IncludedServices,
    IEnumerable<PackageServiceRef> IncludedServiceItems,
    Dictionary<string, Translation> Translations
);

public record PackageServiceRef(string Id, string Name);
