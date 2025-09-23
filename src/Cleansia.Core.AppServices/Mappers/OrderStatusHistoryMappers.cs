using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Mappers;

public static class OrderStatusHistoryMappers
{
    public static OrderStatusTrackDto MapToDto(this OrderStatusTrack statusHistory)
    {
        return new OrderStatusTrackDto(
            Status: statusHistory.Status.MapToCode(),
            CreatedOn: statusHistory.CreatedOn
        );
    }
}