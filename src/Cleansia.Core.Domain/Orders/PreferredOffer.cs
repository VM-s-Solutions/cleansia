using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0045 D7.1 — what the customer is told about their favourite cleaner's reservation. Derived on
/// every read from four values already on the order row, never stored: a derived state has no writer,
/// cannot go stale, needs no backfill, and cannot be left inconsistent by a path nobody remembered —
/// the same reasoning that made ADR-0036 store a deadline rather than a flag.
/// </summary>
[SwaggerEnumAsInt]
public enum PreferredOfferState
{
    /// <summary>
    /// No reservation exists or ever did: no preference, a non-member, a declined resolve outcome, and
    /// the whole 2-8 h notify-only band, where nothing is withheld from anybody.
    /// </summary>
    None = 0,

    /// <summary>The order's first seat is withheld for the chosen cleaner and they have not answered.</summary>
    AwaitingConfirmation = 1,

    /// <summary>The chosen cleaner took the job. Outlives the deadline — nothing clears the pair.</summary>
    Accepted = 2,

    /// <summary>The reservation ended with no confirmation — the deadline passed, or it was declined.</summary>
    Closed = 3,
}

/// <summary>
/// The derivation itself. Pure — four inputs, no collaborators — so the customer DTO, the sweep and any
/// later surface answer the same question the same way.
/// </summary>
public static class PreferredOffer
{
    public static PreferredOfferState StateOf(
        string? preferredEmployeeId,
        DateTime? holdUntilUtc,
        bool beneficiaryIsAssigned,
        DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(preferredEmployeeId) || holdUntilUtc is not { } deadline)
        {
            return PreferredOfferState.None;
        }

        if (beneficiaryIsAssigned)
        {
            return PreferredOfferState.Accepted;
        }

        return deadline > nowUtc ? PreferredOfferState.AwaitingConfirmation : PreferredOfferState.Closed;
    }

    /// <summary>
    /// ADR-0049 — whether the state's sentence is still TRUE of this booking. <see cref="StateOf"/>
    /// answers <i>what became of the reservation</i>; this answers <i>is that still worth saying</i>,
    /// and only the second needs the order. Both are pure, and they stay apart so a fifth column never
    /// leaks into the derivation.
    ///
    /// <para><b>(a) The booking concluded.</b> On <c>Cancelled</c> all four sentences are false — the
    /// booking is open to nobody, nobody is being asked, nothing is confirmed. On <c>Completed</c>,
    /// <i>"open to our whole team"</i> is false. <c>Completed</c> + <c>Accepted</c> is the one true
    /// sentence this limb withholds, and it goes anyway: it is the same fact as the assigned-cleaner
    /// card on the same screen, in a tense that reads as a promise.</para>
    ///
    /// <para><b>(b) The reservation closed and every seat is taken.</b> <i>"This booking is now open to
    /// our whole team"</i> is false when nobody can take it. The term is the FREE SEAT COUNT and never
    /// an assignment count: with <c>RequiredEmployees = ceil(EstimatedTime / 120)</c> most bookings
    /// carry more than one seat, and on a 1-of-2 booking the sentence is still true. This rule removes
    /// false sentences, not stale ones.</para>
    ///
    /// <para>Deliberately NOT limbs: <c>AwaitingConfirmation</c> on a live booking (the deadline term
    /// inside <see cref="StateOf"/> already makes it self-expiring), and <c>Closed</c> on a live but
    /// unfilled booking — true, and the customer's only record of what became of their request.</para>
    /// </summary>
    public static bool IsDisclosable(
        PreferredOfferState state,
        OrderStatus currentStatus,
        int availableSpots)
        => currentStatus is not (OrderStatus.Completed or OrderStatus.Cancelled)
           && !(state == PreferredOfferState.Closed && availableSpots <= 0);

    /// <summary>
    /// Whether a reservation is running right now — the same three columns
    /// <see cref="OrderVisibility.NotHeldFrom"/> reads, asked without a caller.
    /// </summary>
    public static bool HasLiveReservation(
        string? preferredEmployeeId, DateTime? holdUntilUtc, DateTime nowUtc)
        => !string.IsNullOrEmpty(preferredEmployeeId) && holdUntilUtc > nowUtc;
}
