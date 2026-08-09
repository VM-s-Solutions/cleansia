using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

/// <summary>
/// ADR-0045 D7.2 — what the customer sees about their favourite cleaner's reservation, and nothing
/// else. A nested OPTIONAL block on the order detail, so a client that has not been rebuilt is
/// unaffected; an absent block must render as "no offer", never drop the order row.
///
/// <para>The customer is <b>never</b> told which way an offer ended. One state covers a decline and a
/// silence alike: they learn the offer closed and are offered a second choice, never that a named
/// person refused and never that a named person did not answer. (The message's ARRIVAL time does leak
/// "it ended early" against a disclosed deadline — conceded, minimised, not claimed away.)</para>
/// </summary>
public record PreferredOfferDetails(
    PreferredOfferState State,
    /// <summary>The person the customer themselves picked. Never anyone else's name.</summary>
    string? CleanerName,
    /// <summary>
    /// When the reservation ends. Null unless it is running. An INSTANT, not a countdown: a countdown
    /// is a client concern and an anxiety machine; an instant is the fact the customer needs, because
    /// the closure prompt arrives at roughly that time. Rendering it as relative time is the client's
    /// choice.
    /// </summary>
    DateTime? RespondByUtc,
    /// <summary>
    /// Whether naming a second cleaner would be accepted right now — <c>PreferredOfferExit.IsOpen</c>
    /// evaluated read-side, through the SAME function the command gates on, never a re-implementation
    /// and never a client-side lead-time constant. It is a snapshot and may go stale between render and
    /// tap; the command is the gate and the client tolerates the refusal.
    /// </summary>
    bool CanChooseAnother);
