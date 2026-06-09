using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public sealed class OrderPricingCalculator(
    IServiceRepository serviceRepository,
    IPackageRepository packageRepository,
    IExtraRepository extraRepository,
    ICurrencyRepository currencyRepository) : IOrderPricingCalculator
{
    public async Task<OrderPricingResult> CalculateAsync(
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        IEnumerable<string> selectedExtraSlugs,
        int rooms,
        int bathrooms,
        string? currencyId,
        DateTime? cleaningDateUtc,
        CancellationToken cancellationToken)
    {
        var packages = await packageRepository.GetByIds(selectedPackageIds)
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
        decimal extrasSubtotal = 0m;
        if (extraSlugList.Count > 0)
        {
            extrasSubtotal = await extraRepository.GetAll()
                .Where(e => e.IsActive && extraSlugList.Contains(e.Slug))
                .SumAsync(e => e.Price, cancellationToken);
        }

        var currency = string.IsNullOrEmpty(currencyId)
            ? await currencyRepository.GetDefaultAsync(cancellationToken)
            : await currencyRepository.GetByIdAsync(currencyId, cancellationToken);

        var exchangeRate = currency?.ExchangeRate ?? 1m;
        var baseSubtotal = packagesSubtotal + servicesSubtotal + extrasSubtotal;

        // Express surcharge belongs on the pricing side because it's slot-
        // determined, not user-determined. It's a flat +20% on the base
        // subtotal — applied here so the wizard summary line item matches
        // what gets persisted in Order.TotalPrice.
        bool expressSurchargeApplied = false;
        decimal expressSurchargeAmount = 0m;
        if (cleaningDateUtc.HasValue
            && BookingPolicy.RequiresExpressSurcharge(cleaningDateUtc.Value, DateTime.UtcNow))
        {
            expressSurchargeApplied = true;
            expressSurchargeAmount = baseSubtotal * BookingPolicy.ExpressSurchargeRate;
        }

        var totalPrice = (baseSubtotal + expressSurchargeAmount) * exchangeRate;

        return new OrderPricingResult(
            TotalPrice: totalPrice,
            CurrencyId: currency?.Id ?? string.Empty,
            CurrencyCode: currency?.Code ?? string.Empty,
            ServicesSubtotal: servicesSubtotal,
            PackagesSubtotal: packagesSubtotal,
            ExtrasSubtotal: extrasSubtotal,
            ExpressSurchargeApplied: expressSurchargeApplied,
            ExpressSurchargeAmount: expressSurchargeAmount,
            ExchangeRate: exchangeRate);
    }
}
