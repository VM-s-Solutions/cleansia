using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0037 — the ONE rule for "may a cleaner be offered, and take, this order". Every surface reads
/// this type; none re-derives it. A property of the ORDER alone: four columns in, a bool out, and it
/// knows nothing about a cleaner.
///
/// <para>Spans both axes, and a plain status list cannot express it: <c>New</c> is offerable only for
/// cash, and <c>Confirmed</c> only once nothing scheduled can still retract the order. The two
/// evaluation forms below are deliberately NOT one shared expression — SQL and C# disagree on null
/// semantics — and are pinned to each other by an equivalence test over real Postgres, never by
/// review. → /domain/offerability</para>
/// </summary>
public static class OrderAvailability
{
    /// <summary>
    /// The COARSE fulfilment-axis floor — the statuses the rule can ever admit. NOT the rule:
    /// <c>New</c> is conditional. It exists because the clients cannot evaluate the money term
    /// (they filter on none of the three money columns) and because it is the index-served
    /// prefilter on <c>Orders.CurrentStatus</c>.
    /// </summary>
    public static readonly IReadOnlyList<OrderStatus> OfferableStatuses =
        [OrderStatus.New, OrderStatus.Confirmed];

    /// <summary>
    /// Queryable form, composed into <c>OrderSpecification</c> and the digest sweep. <c>CurrentStatus</c>
    /// is NOT NULL, so this is a plain equality on an indexed column and the SQL and C# forms below can
    /// no longer disagree on null semantics for the status term.
    /// </summary>
    public static Expression<Func<Order, bool>> IsOfferableSql { get; } = order =>
        (order.CurrentStatus == OrderStatus.Confirmed
            || (order.CurrentStatus == OrderStatus.New && order.PaymentType == PaymentType.Cash))
        && (order.PaymentStatus == PaymentStatus.Paid
            || (order.PaymentType == PaymentType.Cash && order.RecurringTemplateId == null));

    /// <summary>In-memory form — the <c>TakeOrder</c> write gate. Same rule, C# semantics.</summary>
    public static bool IsOfferable(
        OrderStatus currentStatus,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId) =>
        (currentStatus == OrderStatus.Confirmed
            || (currentStatus == OrderStatus.New && paymentType == PaymentType.Cash))
        && (paymentStatus == PaymentStatus.Paid
            || (paymentType == PaymentType.Cash && recurringTemplateId is null));
}
