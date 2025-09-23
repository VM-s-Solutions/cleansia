using Cleansia.Core.AppServices.Shared.DTOs.Enums;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderStatusTrackDto(
    Code Status,
    DateTimeOffset CreatedOn);