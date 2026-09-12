using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// One service as it was bought, with the price it was bought at.
///
/// <para><b>The money is the point.</b> This was a pure join row — order id, service id, nothing else —
/// so every consumer of a historical order re-read the LIVE catalogue to find out what the line had
/// cost. Three did: the refund allocator, the receipt PDF and the fiscal line items. Raise a service's
/// price in the admin form and a refund on an order placed months earlier paid out a different amount,
/// with no amount shown before it went to Stripe.</para>
///
/// <para><b>Why all three columns and not just the total.</b> <see cref="LineTotal"/> is what the
/// allocator weights by, but the receipt has to print a unit price and a per-room component, and the
/// fiscal line item declares a unit price to a tax authority. Storing only the total would send those
/// two back to the catalogue for the parts, which is the defect.</para>
/// </summary>
public class OrderService : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string ServiceId { get; private set; }
    public Service? Service { get; private set; }

    /// <summary>The service's flat component at the moment of purchase.</summary>
    public decimal UnitBasePrice { get; private set; }

    /// <summary>The per-unit component at the moment of purchase; multiplied by rooms + bathrooms.</summary>
    public decimal UnitPerRoomPrice { get; private set; }

    /// <summary>
    /// <c>UnitBasePrice + UnitPerRoomPrice × (rooms + bathrooms)</c>, resolved at creation.
    ///
    /// <para>Stored rather than recomputed because the room count is on the order and the prices are
    /// here: a reader that multiplied them itself would be a second definition of the line, and the
    /// allocator's ratio only has to agree with the total that was actually charged.</para>
    /// </summary>
    public decimal LineTotal { get; private set; }

    public static OrderService Create(
        Order order,
        Service service,
        decimal unitBasePrice,
        decimal unitPerRoomPrice,
        decimal lineTotal) => new()
    {
        Order = order,
        OrderId = order.Id,
        Service = service,
        ServiceId = service.Id,
        UnitBasePrice = unitBasePrice,
        UnitPerRoomPrice = unitPerRoomPrice,
        LineTotal = lineTotal
    };
}
