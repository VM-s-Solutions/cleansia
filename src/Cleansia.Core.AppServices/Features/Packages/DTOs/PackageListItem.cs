using Cleansia.Core.Domain.Internalization;

namespace Cleansia.Core.AppServices.Features.Packages.DTOs;

public record PackageListItem(
    string Id,
    string Name,
    string Description,
    decimal Price,
    Dictionary<string, Translation> Translations);