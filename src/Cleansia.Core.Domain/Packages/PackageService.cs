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
    /// Dimensionless relative share by which the owning <see cref="Package.Price"/> is split across
    /// the bundle's included services. It is never a currency amount: a service's gross is
    /// <c>PriceWeight / Σ(weights) × Package.Price</c>, keeping <see cref="Package.Price"/> the single
    /// source of truth for the bundle price.
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
