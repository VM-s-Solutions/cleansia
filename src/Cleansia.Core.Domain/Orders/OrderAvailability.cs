using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0037 — the ONE rule for "may a cleaner be offered, and take, this order". Every surface that
/// answers that question reads this type; none re-derives it. Offerability is a property of the
/// ORDER alone: it knows nothing about a cleaner (approval, weekly cap, calendar, the ADR-0036 hold
/// all stay per-caller in <c>TakeOrder</c>), and it takes no collaborators — four columns in, a bool
/// out.
///
/// <para>Two axes, because Cleansia stores them separately and every surface used to consult only
/// the first:</para>
/// <list type="bullet">
///   <item><b>Fulfilment</b> — <c>CurrentStatus</c>: has the work started. <c>Confirmed</c>, plus
///   <c>New</c> for cash only, because on a one-off cash order the take IS the confirmation.</item>
///   <item><b>Money</b> — can anything still retract this order out from under the cleaner we hand
///   it to. There are exactly two scheduled retractors in production and this term is the union of
///   the negations of their own WHERE clauses:
///   <c>CleanupStalePendingOrders</c> (15-min timer, <c>PaymentStatus == Pending AND PaymentType ==
///   Card</c>, no status term) and <c>AutoCancelStaleRecurringOrders</c> (hourly,
///   <c>RecurringTemplateId != null AND PaymentStatus == Pending</c>, no payment-type term). A card
///   order survives only via <c>Paid</c>; a recurring order — cash included — survives only via
///   <c>Paid</c>, which is what the customer's own confirm writes.</item>
/// </list>
///
/// <para><b>Two evaluation forms, deliberately not one shared expression</b> (ADR-0036 precedent):
/// SQL and C# disagree on null semantics and <c>.Compile()</c> on a request path is banned. They
/// are pinned against each other by an equivalence test over real Postgres, never by review.</para>
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
    /// Queryable form, composed into <c>OrderSpecification</c> and the digest sweep. Total over a
    /// NULL <c>CurrentStatus</c>: an equality against a NULL column is UNKNOWN, so a pre-backfill
    /// row is excluded — read surfaces fail CLOSED, which is the conservative direction for a
    /// visibility floor. The write gate deliberately does not (see <see cref="IsOfferable"/>).
    /// </summary>
    public static Expression<Func<Order, bool>> IsOfferableSql { get; } = order =>
        (order.CurrentStatus == OrderStatus.Confirmed
            || (order.CurrentStatus == OrderStatus.New && order.PaymentType == PaymentType.Cash))
        && (order.PaymentStatus == PaymentStatus.Paid
            || (order.PaymentType == PaymentType.Cash && order.RecurringTemplateId == null));

    /// <summary>
    /// In-memory form — the <c>TakeOrder</c> write gate. Same rule, C# semantics.
    ///
    /// <para><paramref name="currentStatus"/> must already be RESOLVED by the caller (the column
    /// when non-null, else the latest history row by CreatedOn desc / Sequence desc). The write
    /// gate must not fail closed on a NULL column the way the read surfaces do, or every legacy
    /// order becomes permanently untakeable. A status that is null after resolution means the order
    /// has no status track at all, and that is not offerable.</para>
    /// </summary>
    public static bool IsOfferable(
        OrderStatus? currentStatus,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId) =>
        (currentStatus == OrderStatus.Confirmed
            || (currentStatus == OrderStatus.New && paymentType == PaymentType.Cash))
        && (paymentStatus == PaymentStatus.Paid
            || (paymentType == PaymentType.Cash && recurringTemplateId is null));
}
