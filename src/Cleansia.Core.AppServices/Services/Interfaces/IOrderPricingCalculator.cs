namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Pricing snapshot returned by <see cref="IOrderPricingCalculator.CalculateAsync"/>.
///
/// <see cref="TotalPrice"/> is the raw subtotal BEFORE any user-level discount
/// (tier, membership, promo) is applied. The express surcharge IS already
/// folded into it when <see cref="ExpressSurchargeApplied"/> is true — the
/// surcharge is a property of the slot, not the user, so it belongs on the
/// pricing side. Discount-aware totals are computed downstream in
/// <c>QuoteOrder.Handler</c> / <c>OrderFactory</c>.
///
/// The broken-out subtotals (<see cref="ServicesSubtotal"/>,
/// <see cref="PackagesSubtotal"/>, <see cref="ExtrasSubtotal"/>,
/// <see cref="ExpressSurchargeAmount"/>) let the customer wizard display a
/// transparent line-item breakdown.
/// </summary>
public record OrderPricingResult(
    decimal TotalPrice,
    string CurrencyId,
    string CurrencyCode,
    decimal ServicesSubtotal,
    decimal PackagesSubtotal,
    decimal ExtrasSubtotal,
    bool ExpressSurchargeApplied,
    decimal ExpressSurchargeAmount,
    decimal ExchangeRate);

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
    /// </summary>
    Task<OrderPricingResult> CalculateAsync(
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        IEnumerable<string> selectedExtraSlugs,
        int rooms,
        int bathrooms,
        string? currencyId,
        DateTime? cleaningDateUtc,
        CancellationToken cancellationToken);
}
