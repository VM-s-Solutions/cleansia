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
    /// Whether a reservation is running right now — the same three columns
    /// <see cref="OrderVisibility.NotHeldFrom"/> reads, asked without a caller.
    /// </summary>
    public static bool HasLiveReservation(
        string? preferredEmployeeId, DateTime? holdUntilUtc, DateTime nowUtc)
        => !string.IsNullOrEmpty(preferredEmployeeId) && holdUntilUtc > nowUtc;
}
