using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// One package as it was bought, with the price it was bought at. See <see cref="OrderService"/> for
/// why the money is here rather than re-read from the catalogue.
///
/// <para>Only a total: unlike a service, a package has no per-room component — its price is flat, and
/// the split across its included services is snapshotted alongside it in
/// <see cref="IncludedServiceLines"/> rather than re-derived from live weights.</para>
/// </summary>
public class OrderPackage : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    /// <summary>What this package actually cost on this order, frozen at creation.</summary>
    public decimal LineTotal { get; private set; }

    private ICollection<OrderPackageService> _includedServiceLines = [];

    /// <summary>
    /// How <see cref="LineTotal"/> was split across the package's included services, frozen at
    /// creation. These sum exactly to <see cref="LineTotal"/>. Empty for a package that includes no
    /// services — that package refunds as a single line.
    /// </summary>
    public IReadOnlyCollection<OrderPackageService> IncludedServiceLines =>
        _includedServiceLines.ToList().AsReadOnly();

    public static OrderPackage Create(Order order, Package package, decimal lineTotal) => new()
    {
        Order = order,
        OrderId = order.Id,
        Package = package,
        PackageId = package.Id,
        LineTotal = lineTotal
    };

    public OrderPackage AddIncludedServiceLines(IEnumerable<OrderPackageService> lines)
    {
        _includedServiceLines = lines.ToList();
        return this;
    }
}
