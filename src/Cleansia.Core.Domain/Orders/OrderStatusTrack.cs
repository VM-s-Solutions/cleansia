using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

public class OrderStatusTrack : Auditable
{
    public OrderStatus Status { get; private set; }

    public string OrderId { get; private set; }

    public Order? Order { get; private set; }

    public static OrderStatusTrack Create(OrderStatus status, Order order) => new()
    {
        Status = status,
        Order = order,
        OrderId = order.Id
    };
}