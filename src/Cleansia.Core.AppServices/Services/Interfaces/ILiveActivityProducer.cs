using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single seam every order-status handler calls to drive the customer's iOS Live Activity
/// (ADR-0029 D2). A SIBLING of <c>INotificationProducer</c>, deliberately NOT a second method on it:
/// an activity update has no feed row and no notification-preference gating — it is glanceable state
/// on its own queue, claim keyspace, and failure domain (ADR-0029 RV-1). Constructing a
/// <c>SendLiveActivityUpdateMessage</c> anywhere but the implementation is a violation, pinned by a
/// raw-file tripwire test.
///
/// The enqueue rides the CALLER's unit of work (the outbox row commits iff the transition commits) —
/// the implementation never commits.
/// </summary>
public interface ILiveActivityProducer
{
    /// <summary>
    /// Enqueues one ActivityKit send for <paramref name="transition"/> — but only when the order's
    /// user holds a registered live-activity token; a transition for a user with no iOS activity
    /// registration produces nothing. <paramref name="eventKey"/> is a
    /// <c>LiveActivityEventKeys</c> value (start/update/end); <paramref name="transition"/> supplies the
    /// idempotency <c>Sequence</c> and the APNs <c>timestamp</c> (<c>CreatedOn</c>).
    /// </summary>
    Task NotifyOrderTransitionAsync(Order order, string eventKey, OrderStatusTrack transition, CancellationToken cancellationToken);
}
