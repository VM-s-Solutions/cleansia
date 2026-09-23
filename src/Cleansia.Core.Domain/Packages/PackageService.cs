using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Packages;

public class PackageService : BaseEntity
{
    public const decimal DefaultPriceWeight = 1m;

    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    public string ServiceId { get; private set; }
    public Service? Service { get; private set; }

    /// <summary>
    /// Dimensionless relative share by which <see cref="PackagePrice.Price"/> in the order's currency
    /// is split across the bundle's included services. A service's gross is
    /// <c>PriceWeight / Σ(weights) × PackagePrice.Price</c>.
    /// </summary>
    public decimal PriceWeight { get; private set; } = DefaultPriceWeight;

    public static PackageService Create(Package package, Service service) => new()
    {
        PackageId = package.Id,
        Package = package,
        ServiceId = service.Id,
        Service = service
    };

    public PackageService SetPriceWeight(decimal priceWeight)
    {
        PriceWeight = priceWeight;
        return this;
    }
}
