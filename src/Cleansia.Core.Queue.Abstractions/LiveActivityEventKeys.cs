using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// The ActivityKit lifecycle events the order-status seam drives (ADR-0029 D2): OnTheWay starts the
/// activity, InProgress updates it, a terminal status (Completed/Cancelled) ends it. The value is the
/// wire <c>event</c> carried on <see cref="Messages.SendLiveActivityUpdateMessage"/> and interpreted
/// by the dispatch consumer (LA-4) — a small frozen vocabulary shared producer↔consumer.
/// </summary>
public static class LiveActivityEventKeys
{
    public const string Start = "start";
    public const string Update = "update";
    public const string End = "end";

    /// <summary>
    /// The activity event a given order status drives, or <c>null</c> for statuses that carry no
    /// activity (New/Pending/Confirmed — the ~8h ActivityKit budget makes a pre-service activity
    /// structurally wrong, ADR-0029 CH-D2-1). Used by <c>AdminOverrideOrderStatus</c>, whose target
    /// status is dynamic; the organic handlers pass the fixed constant for their transition.
    /// </summary>
    public static string? ForStatus(OrderStatus status) => status switch
    {
        OrderStatus.OnTheWay => Start,
        OrderStatus.InProgress => Update,
        OrderStatus.Completed => End,
        OrderStatus.Cancelled => End,
        _ => null,
    };
}
