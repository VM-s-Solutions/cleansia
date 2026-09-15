using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Services.DTOs;

public record ServiceListItem(
    string Id,
    string Name,
    string Description,
    CategoryDto Category,
    decimal BasePrice,
    decimal PerRoomPrice,
    Dictionary<string, Translation> Translations,
    /// <summary>The currency the two prices are in, so a surface labels them from the payload rather than from a default it assumed.</summary>
    string? CurrencyCode = null);
