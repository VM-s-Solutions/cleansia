namespace Cleansia.Core.Clients.Abstractions.Apns;

/// <summary>
/// One ready-to-send ActivityKit payload for one activity token (ADR-0029 D1/D2). Built by the
/// dispatch consumer's payload factory (which knows orders/transitions) and handed to the DUMB
/// <see cref="ILiveActivityPushClient"/> (which knows only JWT signing + APNs). The client owns the
/// wire framing (topic, priority, expiration, the <c>aps</c> envelope); this DTO is the semantic body.
/// </summary>
/// <param name="Event">The ActivityKit <c>event</c>: <c>start</c> | <c>update</c> | <c>end</c>.</param>
/// <param name="ContentState">The S6-allowlisted, versioned content-state (lock-screen visible).</param>
/// <param name="Timestamp">
/// The mandatory APNs <c>timestamp</c> (= the transition's <c>OrderStatusTrack.CreatedOn</c>).
/// ActivityKit discards updates older than the last applied, so a redelivery or cross-transition race
/// can never regress the card.
/// </param>
/// <param name="StaleDate">
/// When the widget flips to its <c>isStale</c> "may be outdated" presentation:
/// <c>max(now + 4h, scheduledEnd + 1h)</c> — a booked-long clean never renders stale mid-service.
/// </param>
/// <param name="DismissalDate">
/// The lock-screen dismissal time on a terminal <c>end</c> event: <c>now + 30 min</c> (Completed —
/// a glanceable receipt that then leaves) or <c>now</c> (Cancelled — a dead order must not linger).
/// Null for non-terminal events.
/// </param>
/// <param name="AttributesType">
/// The ActivityKit attributes type name (e.g. <c>CleanOrderAttributes</c>) — set ONLY on a
/// remote-<c>start</c> (push-to-start) so APNs can materialize the activity; null otherwise.
/// </param>
/// <param name="Attributes">
/// The static activity attributes carried on a remote-<c>start</c> (S6-minimal: order number only);
/// null otherwise.
/// </param>
public sealed record LiveActivityPush(
    string Event,
    LiveActivityContentState ContentState,
    DateTimeOffset Timestamp,
    DateTimeOffset StaleDate,
    DateTimeOffset? DismissalDate,
    string? AttributesType,
    LiveActivityStartAttributes? Attributes);

/// <summary>
/// The static, non-changing attributes an activity is started with (ADR-0029 D4). S6-minimal by type:
/// order number only — no names, addresses, or ids.
/// </summary>
public sealed record LiveActivityStartAttributes(string OrderNumber);
