using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public sealed class OrderPricingCalculator(
    IServiceRepository serviceRepository,
    IPackageRepository packageRepository,
    IExtraRepository extraRepository,
    IServicePriceRepository servicePriceRepository,
    IPackagePriceRepository packagePriceRepository,
    IExtraPriceRepository extraPriceRepository,
    ICurrencyRepository currencyRepository,
    IExpressWaiverResolver expressWaiverResolver) : IOrderPricingCalculator
{
    public async Task<OrderPricingResult> CalculateAsync(
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        IEnumerable<string> selectedExtraSlugs,
        int rooms,
        int bathrooms,
        string? currencyId,
        DateTime? cleaningDateUtc,
        string? userId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // THE CURRENCY IS RESOLVED FIRST, because it is now an input to the prices rather than a label
        // applied afterwards. Prices are AUTHORED per currency (owner ruling 2026-09-08), so there is
        // nothing to price until we know which currency's rows to read.
        var currency = string.IsNullOrEmpty(currencyId)
            ? await currencyRepository.GetDefaultAsync(cancellationToken)
            : await currencyRepository.GetByIdAsync(currencyId, cancellationToken);
        if (currency is null)
        {
            throw new InvalidOperationException(
                $"Cannot price an order: currency '{currencyId}' does not exist.");
        }

        var packages = await packageRepository.GetByIds(selectedPackageIds)
            .ToListAsync(cancellationToken);
        var packagePrices = await CataloguePriceLookup.ForPackagesAsync(
            packagePriceRepository, packages.Select(p => p.Id).ToList(), currency.Id, cancellationToken);
        var packagesSubtotal = packages.Sum(p => PriceOf(packagePrices, p.Id, currency.Code, "package"));

        var services = await serviceRepository.GetByIds(selectedServiceIds)
            .ToListAsync(cancellationToken);
        var servicePrices = await CataloguePriceLookup.ForServicesAsync(
            servicePriceRepository, services.Select(s => s.Id).ToList(), currency.Id, cancellationToken);
        var servicesSubtotal = services.Sum(s =>
        {
            var price = ServicePriceOf(servicePrices, s.Id, currency.Code);
            return price.BasePrice + price.PerRoomPrice * (rooms + bathrooms);
        });

        // Extras are slug-keyed in the catalog because slugs are stable platform-wide
        // (Service/Package only use Ids). Pull active extras by slug — inactive ones
        // are admin-hidden, so a stale client trying to re-quote with one silently
        // drops it instead of erroring (already committed orders preserve the
        // historical slug via Order.Extras).
        var extraSlugList = selectedExtraSlugs?.Distinct().ToList() ?? new List<string>();
        var extraLines = new List<(string Slug, decimal Price)>();
        decimal extrasSubtotal = 0m;
        if (extraSlugList.Count > 0)
        {
            // Materialised rather than summed in the database: the same rows are the
            // quote's extra lines, and a second query to fetch what was just aggregated
            // would be a second chance for the two to disagree.
            var rows = await extraRepository.GetAll()
                .Where(e => e.IsActive && extraSlugList.Contains(e.Slug))
                .Select(e => new { e.Id, e.Slug })
                .ToListAsync(cancellationToken);
            var extraPrices = await CataloguePriceLookup.ForExtrasAsync(
                extraPriceRepository, rows.Select(r => r.Id).ToList(), currency.Id, cancellationToken);
            extraLines = rows
                .Select(r => (r.Slug, Price: PriceOf(extraPrices, r.Id, currency.Code, "extra")))
                .ToList();
            extrasSubtotal = extraLines.Sum(e => e.Price);
        }

        // Built alongside the sums above, from the same values, so a row can never
        // disagree with the subtotal it belongs to.
        var unitCount = rooms + bathrooms;

        var baseSubtotal = packagesSubtotal + servicesSubtotal + extrasSubtotal;

        // Express surcharge belongs on the pricing side because it's slot-
        // determined, not user-determined. It's a flat +20% on the base
        // subtotal — applied here so the wizard summary line item matches
        // what gets persisted in Order.TotalPrice.
        //
        // NO EXCHANGE RATE. Every money figure this method returns is in the catalogue's own currency,
        // because the catalogue is what it is priced from. This used to multiply the whole basket by
        // `currency.ExchangeRate` — a hand-typed admin column with no feed, no history and no snapshot,
        // so editing it silently restated every historical order that referenced it.
        //
        // Owner ruling 2026-09-08 (Option B): a price is AUTHORED per currency, never converted. Wave B
        // replaces the catalogue reads above with a join on per-currency price rows; until then there is
        // exactly one active currency and the identity is the honest scaling.
        var chargeSubtotal = baseSubtotal;

        // PURE READ — the resolver never writes, which is what lets the quote path and the create
        // validator both call it without burning a credit. The reservation happens once, in
        // CreateOrder.Handler, before this price is ever frozen onto an order.
        var waiver = await expressWaiverResolver.ResolveForUserAsync(
            userId, cleaningDateUtc, nowUtc, cancellationToken);

        bool expressSurchargeApplied = false;
        decimal expressSurchargeAmount = 0m;
        if (cleaningDateUtc.HasValue
            && BookingPolicy.RequiresExpressSurcharge(
                cleaningDateUtc.Value, nowUtc, waiverApplies: waiver.Waived))
        {
            expressSurchargeApplied = true;
            expressSurchargeAmount = chargeSubtotal * BookingPolicy.ExpressSurchargeRate;
        }

        var totalPrice = chargeSubtotal + expressSurchargeAmount;

        var lines = new List<OrderPricingLine>();
        foreach (var package in packages.Where(p => p != null))
        {
            var packagePrice = PriceOf(packagePrices, package.Id, currency.Code, "package");
            lines.Add(new OrderPricingLine(
                Kind: "package",
                ItemId: package.Id,
                BaseAmount: packagePrice,
                UnitAmount: 0m,
                Units: 0,
                Amount: packagePrice));
        }

        foreach (var service in services.Where(s => s != null))
        {
            var servicePrice = ServicePriceOf(servicePrices, service.Id, currency.Code);
            var perUnit = servicePrice.PerRoomPrice;
            var basePart = servicePrice.BasePrice;
            lines.Add(new OrderPricingLine(
                Kind: "service",
                ItemId: service.Id,
                BaseAmount: basePart,
                UnitAmount: perUnit,
                Units: unitCount,
                Amount: basePart + perUnit * unitCount));
        }

        if (extraLines.Count > 0)
        {
            foreach (var extra in extraLines)
            {
                lines.Add(new OrderPricingLine(
                    Kind: "extra",
                    ItemId: extra.Slug,
                    BaseAmount: extra.Price,
                    UnitAmount: 0m,
                    Units: 0,
                    Amount: extra.Price));
            }
        }

        return new OrderPricingResult(
            TotalPrice: totalPrice,
            CurrencyId: currency.Id,
            CurrencyCode: currency.Code,
            ServicesSubtotal: servicesSubtotal,
            PackagesSubtotal: packagesSubtotal,
            ExtrasSubtotal: extrasSubtotal,
            ExpressSurchargeApplied: expressSurchargeApplied,
            ExpressSurchargeAmount: expressSurchargeAmount,
            // Always 1: no conversion happens anywhere in this method any more. The field stays on the
            // contract because both mobile clients treat it as required and refuse the page rather than
            // assume parity — deliberately, with tests. It is display-only and never a price input.
            ExchangeRate: 1m,
            ExpressSurchargeWaivedByMembership: waiver.Waived,
            ExpressUpgradesRemaining: waiver.Quota > 0 ? waiver.RemainingBeforeThisBooking : null,
            Lines: lines);
    }

    /// <summary>
    /// FAIL CLOSED. An item with no price row in the requested currency has no price in it, not a free
    /// one. The previous shape coalesced a missing catalogue price to zero, which silently dropped the
    /// line from the total while the customer still received the work.
    ///
    /// <para>It throws rather than returning a result because it is unreachable through the
    /// customer-facing paths: the catalogue withholds unpriced items and the order validators reject
    /// them, so a caller that arrives here has bypassed both. That is a platform bug rather than bad
    /// input, and it mirrors the backstop OrderFactory already keeps for the same shape.</para>
    /// </summary>
    private static decimal PriceOf(
        IReadOnlyDictionary<string, decimal> prices, string itemId, string currencyCode, string kind) =>
        prices.TryGetValue(itemId, out var price)
            ? price
            : throw new InvalidOperationException(
                $"No {kind} price for '{itemId}' in {currencyCode}. It is not offerable in that currency.");

    private static CataloguePriceLookup.ServiceAmount ServicePriceOf(
        IReadOnlyDictionary<string, CataloguePriceLookup.ServiceAmount> prices,
        string serviceId,
        string currencyCode) =>
        prices.TryGetValue(serviceId, out var price)
            ? price
            : throw new InvalidOperationException(
                $"No service price for '{serviceId}' in {currencyCode}. It is not offerable in that currency.");
}
