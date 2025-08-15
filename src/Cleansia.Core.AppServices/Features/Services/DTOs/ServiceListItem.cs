using Cleansia.Core.Domain.Internalization;

namespace Cleansia.Core.AppServices.Features.Services.DTOs;

public record ServiceListItem(
    string Id,
    string Name,
    string Description,
    decimal BasePrice,
    decimal PerRoomPrice,
    Dictionary<string, Translation> Translations);