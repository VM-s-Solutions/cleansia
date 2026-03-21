using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record PackageListItem(
    string Id,
    string Name,
    string Description,
    decimal Price,
    Dictionary<string, Translation> Translations,
    IEnumerable<PackageServiceSummary> IncludedServices);

public record PackageServiceSummary(
    string Name,
    Dictionary<string, Translation> Translations);