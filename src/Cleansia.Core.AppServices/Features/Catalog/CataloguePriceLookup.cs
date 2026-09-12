using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Catalog;

/// <summary>
/// What the catalogue costs in ONE currency — the single place that question is asked, so the
/// fail-closed rule cannot be implemented three slightly different ways.
///
/// <para>Static, with no state and nothing to register, mirroring <c>PayCoverageLookup</c>, which
/// answers the structurally identical question about pay configuration: a set of catalogue entries,
/// and which of them are missing the row that makes them sellable.</para>
///
/// <para><b>A missing row is a gap, never a zero.</b> Under the owner's ruling that prices are
/// AUTHORED per currency rather than converted, an item with no row in the requested currency has no
/// price in that currency — not a free one. Coalescing to zero is how a catalogue silently sells at
/// nothing, and it is what the old <c>?? 0m</c> did on the quote path.</para>
/// </summary>
public static class CataloguePriceLookup
{
    /// <summary>A service's two money components in the requested currency.</summary>
    public readonly record struct ServiceAmount(decimal BasePrice, decimal PerRoomPrice);

    /// <summary>
    /// Prices for the given services, keyed by service id. An id absent from the result has no price
    /// in <paramref name="currencyId"/> and is therefore not offerable in it.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, ServiceAmount>> ForServicesAsync(
        IServicePriceRepository repository,
        IReadOnlyCollection<string> serviceIds,
        string currencyId,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return new Dictionary<string, ServiceAmount>();
        }

        var rows = await repository.GetAll()
            .Where(p => p.CurrencyId == currencyId && serviceIds.Contains(p.ServiceId))
            .Select(p => new { p.ServiceId, p.BasePrice, p.PerRoomPrice })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ServiceId, r => new ServiceAmount(r.BasePrice, r.PerRoomPrice));
    }

    /// <summary>Prices for the given packages, keyed by package id. Absent means not offerable.</summary>
    public static async Task<IReadOnlyDictionary<string, decimal>> ForPackagesAsync(
        IPackagePriceRepository repository,
        IReadOnlyCollection<string> packageIds,
        string currencyId,
        CancellationToken cancellationToken)
    {
        if (packageIds.Count == 0)
        {
            return new Dictionary<string, decimal>();
        }

        var rows = await repository.GetAll()
            .Where(p => p.CurrencyId == currencyId && packageIds.Contains(p.PackageId))
            .Select(p => new { p.PackageId, p.Price })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.PackageId, r => r.Price);
    }

    /// <summary>Prices for the given extras, keyed by extra id. Absent means not offerable.</summary>
    public static async Task<IReadOnlyDictionary<string, decimal>> ForExtrasAsync(
        IExtraPriceRepository repository,
        IReadOnlyCollection<string> extraIds,
        string currencyId,
        CancellationToken cancellationToken)
    {
        if (extraIds.Count == 0)
        {
            return new Dictionary<string, decimal>();
        }

        var rows = await repository.GetAll()
            .Where(p => p.CurrencyId == currencyId && extraIds.Contains(p.ExtraId))
            .Select(p => new { p.ExtraId, p.Price })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.ExtraId, r => r.Price);
    }
}
