namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

/// <summary>
/// ADR-0045 D9 — one job waiting for this cleaner's answer. Deliberately NOT the ordinary board row:
/// the whole point of the surface is that these are not "jobs you could take", they are "jobs held for
/// you until <see cref="RespondByUtc"/>", which is the only thing the reservation adds and the reason
/// the perk read as priority rather than assignment until now.
///
/// <para>Carries no other cleaner's identity and no customer PII beyond the coarse
/// <see cref="CustomerAddressApproximate"/> the pre-acceptance board already shows — the cleaner has
/// not accepted yet.</para>
/// </summary>
public record PendingOfferItem(
    string Id,
    string DisplayOrderNumber,
    DateTime CleaningDateTime,
    int EstimatedTime,
    /// <summary>
    /// The instant the reservation ends, as an INSTANT and never a countdown: a countdown is a client
    /// concern and an anxiety machine, an instant is a fact. Equal to the customer's own
    /// <c>respondByUtc</c> on the same order, so the two sides never see different deadlines.
    /// </summary>
    DateTime RespondByUtc,
    string CustomerAddressApproximate,
    int Rooms,
    int Bathrooms,
    decimal TotalPrice,
    string CurrencyCode);
