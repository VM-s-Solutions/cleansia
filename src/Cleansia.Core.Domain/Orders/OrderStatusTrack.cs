using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

public class OrderStatusTrack : Auditable, ITenantEntity
{
    public OrderStatus Status { get; private set; }

    public string OrderId { get; private set; }

    public Order? Order { get; private set; }

    // Strictly-increasing append index within the owning order. CreatedOn is millisecond-resolution and
    // ties when two transitions land in the same tick; Sequence is the deterministic tiebreaker that makes
    // "current status" correct by construction (the order is the consistency boundary — assigned in
    // Order.AddOrderStatus, never set by the caller).
    public int Sequence { get; private set; }

    public static OrderStatusTrack Create(OrderStatus status, Order order) => new()
    {
        Status = status,
        Order = order,
        OrderId = order.Id
    };

    internal void AssignSequence(int sequence) => Sequence = sequence;
}