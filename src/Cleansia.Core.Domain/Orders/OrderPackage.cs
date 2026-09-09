using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// One package as it was bought, with the price it was bought at. See <see cref="OrderService"/> for
/// why the money is here rather than re-read from the catalogue.
///
/// <para>Only a total: unlike a service, a package has no per-room component — its price is flat and
/// the split across its included services is derived from their weights.</para>
///
/// <para><b>Known gap, deliberately not closed here.</b> The refund allocator splits this total across
/// the package's included services using <c>PackageService.PriceWeight</c>, which is still read LIVE
/// and IS editable through the admin package form. So snapshotting the total stops a re-PRICED package
/// from restating a historical refund, but a re-WEIGHTED or re-COMPOSED one still moves the split
/// between its lines. Reported rather than absorbed — it needs its own decision about whether the
/// weights are snapshotted too.</para>
/// </summary>
public class OrderPackage : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    /// <summary>What this package actually cost on this order, frozen at creation.</summary>
    public decimal LineTotal { get; private set; }

    public static OrderPackage Create(Order order, Package package, decimal lineTotal) => new()
    {
        Order = order,
        OrderId = order.Id,
        Package = package,
        PackageId = package.Id,
        LineTotal = lineTotal
    };
}
