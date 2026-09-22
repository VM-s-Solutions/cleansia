using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// One service inside a bought package, with the share of the package's price it was sold for.
///
/// <para><b>Why this exists.</b> A package's price is split across its included services by their
/// relative <c>PackageService.PriceWeight</c>, and that split is what the refund allocator weights a
/// bundled line by. Snapshotting only the package TOTAL was not enough: the weights, and the very
/// composition of the package, are editable through the shipped admin package form. So a re-weighted
/// or re-composed package still moved the split between the lines of an order placed months earlier —
/// the same defect as reading a live price, one level down.</para>
///
/// <para>The DERIVED GROSS is stored rather than the weight. A weight is only meaningful against the
/// other weights in the same package at the same moment, so storing it would leave the reader to
/// re-derive a split from a set that may since have gained or lost a member. The share is the number
/// the money question actually asks for, and these sum exactly to the package's
/// <see cref="OrderPackage.LineTotal"/>.</para>
/// </summary>
public class OrderPackageService : BaseEntity
{
    public string OrderPackageId { get; private set; }
    public OrderPackage? OrderPackage { get; private set; }

    public string ServiceId { get; private set; }
    public Service? Service { get; private set; }

    /// <summary>
    /// This service's share of the package line, frozen at creation. The shares of one package sum
    /// exactly to that package's <see cref="OrderPackage.LineTotal"/> — the last one absorbs the
    /// sub-cent residual, as the live split always did.
    /// </summary>
    public decimal LineGross { get; private set; }

    public static OrderPackageService Create(OrderPackage orderPackage, string serviceId, decimal lineGross) => new()
    {
        OrderPackage = orderPackage,
        OrderPackageId = orderPackage.Id,
        ServiceId = serviceId,
        LineGross = lineGross
    };
}
