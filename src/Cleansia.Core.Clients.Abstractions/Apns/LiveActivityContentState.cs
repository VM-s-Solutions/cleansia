namespace Cleansia.Core.Clients.Abstractions.Apns;

/// <summary>
/// The versioned, cross-platform content-state wire contract (ADR-0029 D4) — the lock-screen-visible
/// ActivityKit state, decoded 1:1 by the iOS widget's <c>ContentState: Codable</c>. A shared JSON
/// fixture pins it on BOTH sides; evolution is additive-only.
///
/// <para><b>S6 allowlist, enforced STRUCTURALLY:</b> the type carries EXACTLY
/// <c>{v, status, orderNumber, scheduledStart, scheduledEnd}</c> and nothing else. Names (customer AND
/// cleaner), addresses, free text, and internal ids (<c>orderId</c> stays app-side) can never reach the
/// payload because there is no field to hold them. <c>status</c> is a STRING the widget maps unknown
/// values of to a generic in-service presentation, so schema evolution can never fail decoding.</para>
/// </summary>
public sealed record LiveActivityContentState(
    int V,
    string Status,
    string OrderNumber,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd);
