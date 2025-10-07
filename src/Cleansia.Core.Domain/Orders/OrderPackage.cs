using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Packages;

namespace Cleansia.Core.Domain.Orders;

public class OrderPackage : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    public static OrderPackage Create(Order order, Package package) => new()
    {
        Order = order,
        OrderId = order.Id,
        Package = package,
        PackageId = package.Id
    };
}