using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public sealed class OrderPricingCalculator(
    IServiceRepository serviceRepository,
    IPackageRepository packageRepository,
    IExtraRepository extraRepository,
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
        var packages = await packageRepository.GetByIds(selectedPackageIds)
            .Include(p => p.IncludedServices)
            .ThenInclude(p => p.Service)
            .ToListAsync(cancellationToken);
        var packagesSubtotal = packages.Sum(p => p.Price);

        var services = await serviceRepository.GetByIds(selectedServiceIds)
            .ToListAsync(cancellationToken);
        var servicesSubtotal = services.Sum(s => s?.BasePrice + s?.PerRoomPrice * (rooms + bathrooms)) ?? 0m;

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
                .Select(e => new { e.Slug, e.Price })
                .ToListAsync(cancellationToken);
            extraLines = rows.Select(r => (r.Slug, r.Price)).ToList();
            extrasSubtotal = extraLines.Sum(e => e.Price);
        }

        // Built alongside the sums above, from the same values, so a row can never
        // disagree with the subtotal it belongs to.
        var unitCount = rooms + bathrooms;

        var currency = string.IsNullOrEmpty(currencyId)
            ? await currencyRepository.GetDefaultAsync(cancellationToken)
            : await currencyRepository.GetByIdAsync(currencyId, cancellationToken);

        var exchangeRate = currency?.ExchangeRate ?? 1m;
        var baseSubtotal = packagesSubtotal + servicesSubtotal + extrasSubtotal;

        // Express surcharge belongs on the pricing side because it's slot-
        // determined, not user-determined. It's a flat +20% on the base
        // subtotal — applied here so the wizard summary line item matches
        // what gets persisted in Order.TotalPrice.
        //
        // EVERY money figure this method returns is in the CHARGE currency — the catalog is priced in
        // the base one, so the scaling happens here and exactly once. CreateOrder.Handler derives its
        // discount base as TotalPrice - ExpressSurchargeAmount, so an unscaled surcharge would be
        // subtracted from a scaled total and inflate every discounted price at any exchange rate but 1;
        // the broken-out line items render under this quote's own CurrencyCode, so an unscaled one
        // prints a base-currency number under the charge currency's symbol.
        var chargeSubtotal = baseSubtotal * exchangeRate;

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

        // Scaled by the same exchangeRate as every other money figure this method
        // returns, and only after it is known.
        var lines = new List<OrderPricingLine>();
        foreach (var package in packages.Where(p => p != null))
        {
            lines.Add(new OrderPricingLine(
                Kind: "package",
                ItemId: package.Id,
                BaseAmount: package.Price * exchangeRate,
                UnitAmount: 0m,
                Units: 0,
                Amount: package.Price * exchangeRate));
        }

        foreach (var service in services.Where(s => s != null))
        {
            var perUnit = service.PerRoomPrice * exchangeRate;
            var basePart = service.BasePrice * exchangeRate;
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
                    BaseAmount: extra.Price * exchangeRate,
                    UnitAmount: 0m,
                    Units: 0,
                    Amount: extra.Price * exchangeRate));
            }
        }

        return new OrderPricingResult(
            TotalPrice: totalPrice,
            CurrencyId: currency?.Id ?? string.Empty,
            CurrencyCode: currency?.Code ?? string.Empty,
            ServicesSubtotal: servicesSubtotal * exchangeRate,
            PackagesSubtotal: packagesSubtotal * exchangeRate,
            ExtrasSubtotal: extrasSubtotal * exchangeRate,
            ExpressSurchargeApplied: expressSurchargeApplied,
            ExpressSurchargeAmount: expressSurchargeAmount,
            ExchangeRate: exchangeRate,
            ExpressSurchargeWaivedByMembership: waiver.Waived,
            ExpressUpgradesRemaining: waiver.Quota > 0 ? waiver.RemainingBeforeThisBooking : null,
            Lines: lines,
            EstimatedDurationMinutes: OrderDuration.EstimateMinutes(services, packages));
    }
}
