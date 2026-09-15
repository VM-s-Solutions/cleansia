using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// One extra as it was bought — the row that replaced <c>Orders.Extras</c>, a
/// <c>Dictionary&lt;string, bool&gt;</c> stored as a JSON column with no price in it at all.
///
/// <para><b>Why a table.</b> The JSON column recorded WHICH extras were chosen and nothing about what
/// they cost, so every consumer of a historical order had to go back to the live catalogue to find out
/// — including the refund allocator. Edit an extra's price in the admin form and every past order's
/// refund split moved. It also had no foreign key, so deleting a catalogue row a historical invoice
/// referenced was a database no-op.</para>
///
/// <para><b>A row means the extra was selected. There is no false.</b> The dictionary could carry
/// <c>false</c>, which meant "offered and declined" nowhere and "selected" everywhere that read it —
/// <c>Validator.SelectedExtraSlugsFrom</c> filtered on the value and the pricing calculator only ever
/// saw the true ones. Absence is the honest encoding, and it is the one a table can express.</para>
///
/// <para><b><see cref="Slug"/> is snapshotted, not looked up.</b> It is the only identifier of an extra
/// that ever crosses the wire — five separate client-side maps are keyed by the slug literal, and the
/// partner app persists a cleaner's per-order checklist under it. A renamed or deactivated catalogue
/// row must still render on the order that bought it, so the slug is copied in at creation and read
/// back verbatim.</para>
///
/// <para>Derives <see cref="BaseEntity"/> rather than <c>Auditable</c>, exactly like its
/// <see cref="OrderService"/> and <see cref="OrderPackage"/> siblings: no <c>TenantId</c>, therefore no
/// global query filter and no nullable-tenant term in the unique index.</para>
/// </summary>
public class OrderExtra : BaseEntity
{
    public string OrderId { get; private set; }
    public Order? Order { get; private set; }

    public string ExtraId { get; private set; }
    public Extra? Extra { get; private set; }

    /// <summary>
    /// The catalogue slug as it stood when the order was placed. Snapshot — see the class note.
    /// </summary>
    public string Slug { get; private set; }

    /// <summary>
    /// What this extra actually cost on this order, in the order's currency. Frozen at creation, so no
    /// later catalogue edit can restate a receipt or a refund.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    public static OrderExtra Create(Order order, Extra extra, decimal unitPrice) => new()
    {
        Order = order,
        OrderId = order.Id,
        Extra = extra,
        ExtraId = extra.Id,
        Slug = extra.Slug,
        UnitPrice = unitPrice
    };
}
