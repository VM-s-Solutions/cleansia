using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.TestUtilities.MockDataFactories.Orders;

/// <summary>
/// An order line carrying an arbitrary price, for the suites where the line MONEY is not the subject.
///
/// <para>Order lines snapshot their own price now — a catalogue entry has no price of its own, it has
/// a price per currency — so every suite that attaches a line has to name an amount, including the many
/// that attach one only to have something for the assertion to walk over. These are those. The amounts
/// are constants rather than parameters precisely so that a reader can see at a glance that nothing
/// here depends on them.</para>
///
/// <para><b>Not for the money suites.</b> Refund allocation, package weight splits and the pricing
/// calculator all judge these numbers, and a shared constant would put the expected value and the
/// actual one on the same side of the assertion. Those suites write their amounts inline, where the
/// hand-derived expectation can sit next to them.</para>
/// </summary>
public static class OrderLineMockFactory
{
    public const decimal BasePrice = 500m;

    public const decimal PerRoomPrice = 100m;

    public const decimal PackagePrice = 1000m;

    public static OrderService ServiceLine(Order order, Service service) =>
        OrderService.Create(
            order,
            service,
            unitBasePrice: BasePrice,
            unitPerRoomPrice: PerRoomPrice,
            lineTotal: BasePrice + PerRoomPrice * (order.Rooms + order.Bathrooms));

    public static OrderPackage PackageLine(Order order, Package package) =>
        OrderPackage.Create(order, package, lineTotal: PackagePrice);
}
