namespace Cleansia.Core.Clients.Abstractions.Apns;

/// <summary>
/// The versioned, cross-platform content-state wire contract (ADR-0029 D4) — the lock-screen-visible
/// ActivityKit state, decoded 1:1 by the iOS widget's <c>ContentState: Codable</c>. A shared JSON
/// fixture pins it on BOTH sides; evolution is additive-only.
///
/// <para><b>S6 allowlist, enforced STRUCTURALLY:</b> the type carries EXACTLY
/// <c>{v, status, orderNumber, scheduledStart, scheduledEnd, phaseStart, phaseEnd}</c> and nothing else.
/// Names (customer AND cleaner), addresses, free text, and internal ids (<c>orderId</c> stays app-side)
/// can never reach the payload because there is no field to hold them — the two phase fields are order
/// timestamps, not personal data. <c>status</c> is a STRING the widget maps unknown values of to a
/// generic in-service presentation, so schema evolution can never fail decoding.</para>
///
/// <para><see cref="PhaseStart"/>/<see cref="PhaseEnd"/> are the ACTUAL phase window (when the cleaner
/// really went on-the-way / started), as opposed to the BOOKED <c>scheduled*</c> window: a countdown
/// anchored to the booked window clamps at 00:00 the moment that window elapses, and the card freezes.
/// They are optional in BOTH directions — omitted (never serialized) when null, so an older iOS build
/// decodes the payload unchanged and a newer build tolerates a payload built before this field pair.</para>
/// </summary>
public sealed record LiveActivityContentState(
    int V,
    string Status,
    string OrderNumber,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    DateTimeOffset? PhaseStart = null,
    DateTimeOffset? PhaseEnd = null);
