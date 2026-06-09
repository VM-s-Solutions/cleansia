namespace Cleansia.Core.AppServices.Features.Packages;

/// <summary>
/// Splits a package line's gross across its included services by their relative
/// <c>PackageService.PriceWeight</c>. <c>Package.Price</c> stays the single source of truth: the
/// derived grosses always sum back to the package line gross — changing weights only redistributes
/// shares, it never introduces a second price source or rounding drift.
/// </summary>
public static class PackagePricing
{
    /// <summary>
    /// For each included service (in the given order) returns its gross as its weight-share of
    /// <paramref name="packageLineGross"/>, each rounded to 2 decimals. The last service absorbs the
    /// sub-cent residual so the result sums exactly to <paramref name="packageLineGross"/>.
    /// </summary>
    public static IReadOnlyList<decimal> DeriveIncludedServiceGrosses(
        IReadOnlyList<decimal> priceWeights,
        decimal packageLineGross)
    {
        if (priceWeights.Count == 0)
        {
            return [];
        }

        var totalWeight = priceWeights.Sum();
        if (totalWeight <= 0m)
        {
            throw new ArgumentException("The sum of price weights must be positive.", nameof(priceWeights));
        }

        var grosses = new decimal[priceWeights.Count];
        decimal allocated = 0m;
        for (var i = 0; i < priceWeights.Count - 1; i++)
        {
            grosses[i] = Math.Round(priceWeights[i] / totalWeight * packageLineGross, 2, MidpointRounding.AwayFromZero);
            allocated += grosses[i];
        }

        grosses[^1] = packageLineGross - allocated;
        return grosses;
    }
}
