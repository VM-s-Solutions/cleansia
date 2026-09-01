namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Pricing snapshot. <see cref="TotalPrice"/> is the RAW subtotal <b>before any user-level discount</b>
/// — but the express surcharge IS already folded in, because the surcharge is a property of the slot
/// rather than the user. Discount-aware totals are computed downstream.
/// → /product/business-rules#price-stages
/// </summary>
/// <param name="ExpressSurchargeWaivedByMembership">
/// The slot IS inside the express window and the surcharge was nevertheless not charged, because the
/// caller's membership carries a free express upgrade and one was available. Without this,
/// <c>ExpressSurchargeApplied: false, ExpressSurchargeAmount: 0</c> is indistinguishable from "this is
/// not an express slot at all", and no client can render the waiver instead of the surcharge.
/// </param>
/// <param name="ExpressUpgradesRemaining">
/// Live waivers left in the current period BEFORE this booking — the single definition (a client that
/// wants "after" computes <c>remaining - (waived ? 1 : 0)</c>). Null for a caller with no membership.
/// </param>
/// <summary>
/// One priced row of the quote, in the CHARGE currency, so the customer can be shown
/// where a number came from instead of only what it totals.
///
/// It is computed here rather than reconstructed in the browser for two reasons. The
/// catalogue's <c>BasePrice</c>/<c>PerRoomPrice</c> reach the client UNSCALED, in the
/// base currency, while every money field on the quote is in the charge currency — so a
/// client-side breakdown is silently wrong at any exchange rate but 1. And a formula
/// copied into a client is a second definition of the price that has to be kept in step
/// with this one forever.
/// </summary>
/// <param name="Kind">"package", "service" or "extra".</param>
/// <param name="ItemId">Package/Service id, or an Extra's slug. The client already holds
/// the catalogue and resolves the display name from it, so no name is sent — and none is
/// translated on this side.</param>
/// <param name="BaseAmount">The flat part of the row.</param>
/// <param name="UnitAmount">Charged per unit, 0 when the row does not scale.</param>
/// <param name="Units">How many units the row was charged for. For a service this is
/// rooms + bathrooms: there is ONE per-unit rate and a bathroom costs what a room costs,
/// so presenting it as two rates would imply a distinction the pricing does not make.</param>
public record OrderPricingLine(
    string Kind,
    string ItemId,
    decimal BaseAmount,
    decimal UnitAmount,
    int Units,
    decimal Amount);

public record OrderPricingResult(
    decimal TotalPrice,
    string CurrencyId,
    string CurrencyCode,
    decimal ServicesSubtotal,
    decimal PackagesSubtotal,
    decimal ExtrasSubtotal,
    bool ExpressSurchargeApplied,
    decimal ExpressSurchargeAmount,
    decimal ExchangeRate,
    bool ExpressSurchargeWaivedByMembership = false,
    int? ExpressUpgradesRemaining = null,
    IReadOnlyList<OrderPricingLine>? Lines = null);

public interface IOrderPricingCalculator
{
    /// <summary>
    /// Compute the raw pre-discount price. Extras are priced from the catalog at call time.
    /// <paramref name="cleaningDateUtc"/> drives the express rule; pass null to skip it (the initial
    /// wizard quote, before a slot is picked). <paramref name="userId"/> makes the waiver previewable —
    /// guests preview none, and <b>nothing is consumed here on any path</b>.
    ///
    /// <para><b><paramref name="nowUtc"/> is captured ONCE by the caller and threaded, never read from
    /// the clock inside.</b> The express window is [2h, 4h) and lead time shrinks while a request runs, so
    /// two clock reads can put the resolver and the policy on opposite sides of the boundary — a reserved
    /// slot on an order whose persisted price carries no surcharge, which no release rule and no orphan
    /// sweep can see. → /product/business-rules#price-stages</para>
    /// </summary>
    Task<OrderPricingResult> CalculateAsync(
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        IEnumerable<string> selectedExtraSlugs,
        int rooms,
        int bathrooms,
        string? currencyId,
        DateTime? cleaningDateUtc,
        string? userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
