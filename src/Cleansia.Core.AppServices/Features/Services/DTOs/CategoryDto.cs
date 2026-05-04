using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Services.DTOs;

public record CategoryDto(
    string Id,
    string Slug,
    string Name,
    string? Description,
    int DisplayOrder,
    Dictionary<string, Translation>? Translations);
