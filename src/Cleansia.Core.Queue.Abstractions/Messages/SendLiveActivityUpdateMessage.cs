namespace Cleansia.Core.Queue.Abstractions.Messages;

/// <summary>
/// Queue message enqueued by <c>LiveActivityProducer</c> (the ONLY construction site — a raw-file
/// tripwire pins that) when an order transitions, driving one ActivityKit send on the
/// <c>live-activity-dispatch</c> queue (ADR-0029 D2). A SIBLING of
/// <see cref="SendPushNotificationMessage"/>, never a second push: a Live Activity is glanceable
/// state (its own queue, claim keyspace, and failure domain), not a feed/preference-gated alert.
///
/// <see cref="EventKey"/> is one of <see cref="LiveActivityEventKeys"/> (start/update/end).
/// <see cref="TransitionAtUtc"/> becomes the APNs payload's mandatory <c>timestamp</c> — ActivityKit
/// discards out-of-order updates, so a redelivery or cross-transition race can never regress the card.
///
/// S6: the fields are the S6 allowlist and NOTHING more — no customer/cleaner name, address, free
/// text, or internal id (<c>OrderId</c> stays app-side on iOS). The payload is lock-screen visible.
/// <see cref="TenantId"/> is carried because the queue consumer has no JWT (it sets the tenant
/// override before reading tenant-scoped token rows).
/// </summary>
public record SendLiveActivityUpdateMessage(
    string UserId,
    string OrderId,
    string EventKey,
    string OrderNumber,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    DateTimeOffset TransitionAtUtc,
    string? TenantId);
