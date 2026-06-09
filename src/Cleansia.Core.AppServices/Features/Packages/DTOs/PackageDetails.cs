namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record PackageDetails(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int EstimatedTime,
    string CurrencyCode,
    IEnumerable<string> IncludedServices,
    IEnumerable<PackageServiceRef> IncludedServiceItems
);

public record PackageServiceRef(string Id, string Name);
