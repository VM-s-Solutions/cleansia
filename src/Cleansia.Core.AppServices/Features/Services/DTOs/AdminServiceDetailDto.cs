using Cleansia.Core.Domain.Internationalization;

namespace Cleansia.Core.AppServices.Features.Services.DTOs;

public record AdminServiceDetailDto(
    string Id,
    string Name,
    string Description,
    decimal BasePrice,
    decimal PerRoomPrice,
    int EstimatedTime,
    Dictionary<string, Translation> Translations,
    DateTimeOffset CreatedOn,
    DateTimeOffset? UpdatedOn);