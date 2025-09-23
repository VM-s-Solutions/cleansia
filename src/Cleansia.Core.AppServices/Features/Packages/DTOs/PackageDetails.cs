namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record PackageDetails(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int EstimatedTime,
    string CurrencyCode,
    IEnumerable<string> IncludedServices
);