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
    int? ExpressUpgradesRemaining = null);

public interface IOrderPricingCalculator
{
    /// <summary>
    /// Compute the raw pre-discount price for a quote / order.
    /// <paramref name="selectedExtraIds"/> are <see cref="Cleansia.Core.Domain.Orders.Extra.Slug"/>
    /// values keyed by catalog entry — they're priced from the
    /// <c>Extras</c> table at call time so a price change on an admin-only
    /// extra reflects immediately in quotes.
    /// <paramref name="cleaningDateUtc"/> drives the express-surcharge rule
    /// (<see cref="Cleansia.Core.AppServices.Features.Orders.BookingPolicy.RequiresExpressSurcharge"/>).
    /// Pass <see langword="null"/> to skip the surcharge check (used by the
    /// initial wizard quote before the user has picked a slot).
    ///
    /// <para><paramref name="userId"/> is what makes the express waiver previewable: the calculator asks
    /// the PURE resolver whether this caller's membership waives the surcharge. Null / empty = guest, and
    /// no waiver is ever previewed. Nothing is consumed here, on any path.</para>
    ///
    /// <para><paramref name="nowUtc"/> is captured ONCE per request by the caller and threaded, rather
    /// than read from the clock inside. The express window is [2h, 4h) and lead time shrinks while a
    /// request runs, so two clock reads can put the resolver and the policy on opposite sides of the 4h
    /// boundary — a reserved slot on an order whose persisted price carries no surcharge, which no
    /// release rule and no orphan sweep can see.</para>
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
