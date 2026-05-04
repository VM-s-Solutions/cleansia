using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Services;

public sealed class OrderPricingCalculator(
    IServiceRepository serviceRepository,
    IPackageRepository packageRepository,
    ICurrencyRepository currencyRepository) : IOrderPricingCalculator
{
    public async Task<OrderPricingResult> CalculateAsync(
        IEnumerable<string> selectedServiceIds,
        IEnumerable<string> selectedPackageIds,
        int rooms,
        int bathrooms,
        string? currencyId,
        CancellationToken cancellationToken)
    {
        var packages = await packageRepository.GetByIds(selectedPackageIds)
            .ToListAsync(cancellationToken);
        var packagesSubtotal = packages.Sum(p => p.Price);

        var services = await serviceRepository.GetByIds(selectedServiceIds)
            .ToListAsync(cancellationToken);
        var servicesSubtotal = services.Sum(s => s?.BasePrice + s?.PerRoomPrice * (rooms + bathrooms)) ?? 0m;

        var currency = string.IsNullOrEmpty(currencyId)
            ? await currencyRepository.GetDefaultAsync(cancellationToken)
            : await currencyRepository.GetByIdAsync(currencyId, cancellationToken);

        var exchangeRate = currency?.ExchangeRate ?? 1m;
        var totalPrice = (packagesSubtotal + servicesSubtotal) * exchangeRate;

        return new OrderPricingResult(
            TotalPrice: totalPrice,
            CurrencyId: currency?.Id ?? string.Empty,
            CurrencyCode: currency?.Code ?? string.Empty,
            ServicesSubtotal: servicesSubtotal,
            PackagesSubtotal: packagesSubtotal,
            ExchangeRate: exchangeRate);
    }
}
