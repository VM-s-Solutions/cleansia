using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0037 — the ONE rule for "may a cleaner be offered, and take, this order". Every surface reads
/// this type; none re-derives it. A property of the ORDER alone: four columns in, a bool out, and it
/// knows nothing about a cleaner.
///
/// <para>Spans both axes, and a plain status list cannot express it: the fulfilment term says the work
/// is not over, and the MONEY term carries the whole payment qualification on its own. The two
/// evaluation forms below are deliberately NOT one shared expression — SQL and C# disagree on null
/// semantics — and are pinned to each other by an equivalence test over real Postgres, never by
/// review. → /domain/offerability</para>
///
/// <para><b>The status term no longer qualifies payment</b> (owner ruling 2026-09-08, T-0691). It used
/// to admit <c>New</c> for <c>Cash</c> only, because a paid CARD order reached this rule by being
/// <c>Confirmed</c> — the Stripe webhook wrote that status when money settled. Now <c>Confirmed</c>
/// means only "a cleaner took this job", so a paid card order rests at <c>New</c> and the cash
/// qualifier would have taken every card job off every board. Dropping it is not a widening: the money
/// term below already refuses <c>New + Card + Pending</c> and <c>New + Card + Failed</c>, so exactly
/// one input flips — <c>New + Card + Paid</c>, which is the state the ruling creates.</para>
///
/// <para><b>The fulfilment axis says the work is not OVER — not that it has not STARTED</b> (owner
/// ruling 2026-09-06). A crew is <c>ceil(EstimatedTime / 120)</c> and the catalogue carries single
/// 180- and 240-minute services, so a two-seat job is an ordinary booking — and <c>NotifyOnTheWay</c>
/// writes an ORDER-level <c>OnTheWay</c> the moment ONE cleaner sets off. While "started" meant "not
/// offerable", that single tap took a half-crewed job off every board and made <c>TakeOrder</c> refuse
/// it with the other seat still empty. Only <c>Completed</c>, <c>Cancelled</c> and the dead
/// <c>Pending</c> are out.</para>
///
/// <para><b>It still does not read seats, and must not.</b> "While a seat remains" is
/// <c>Order.HasAvailableSpots</c>, and every consumer already conjoins it for itself —
/// <c>PreferredOfferExit</c> by the stricter <c>AssignedEmployees.Count == 0</c>. Folding it in here
/// would make this type read a collection, force a join into both evaluation forms, and break
/// <c>TakeOrder</c>'s offerability probe, which projects exactly these four scalars.</para>
/// </summary>
public static class OrderAvailability
{
    /// <summary>
    /// The COARSE fulfilment-axis floor — the statuses the rule can ever admit. NOT the rule:
    /// <c>New</c> is conditional. It exists because the clients cannot evaluate the money term
    /// (they filter on none of the three money columns) and because it is the index-served
    /// prefilter on <c>Orders.CurrentStatus</c>.
    ///
    /// <para><b>Keep it an ARRAY.</b> <c>OrderSpecification</c> spends it as <c>Contains</c>, which EF
    /// emits as <c>= ANY (@p)</c> — the leading index condition on
    /// <c>IX_Orders_CurrentStatus_CleaningDateTime</c>. Re-expressing these members as an OR over
    /// <c>CurrentStatus</c> keeps the index but demotes the term to a residual filter, which is why the
    /// array survives ALONGSIDE the OR below and must not be "simplified" away as redundant.</para>
    /// </summary>
    public static readonly IReadOnlyList<OrderStatus> OfferableStatuses =
        [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay, OrderStatus.InProgress];

    /// <summary>
    /// Queryable form, composed into <c>OrderSpecification</c> and the digest sweep. <c>CurrentStatus</c>
    /// is NOT NULL, so this is a plain equality on an indexed column and the SQL and C# forms below can
    /// no longer disagree on null semantics for the status term.
    /// </summary>
    public static Expression<Func<Order, bool>> IsOfferableSql { get; } = order =>
        (order.CurrentStatus == OrderStatus.Confirmed
            || order.CurrentStatus == OrderStatus.OnTheWay
            || order.CurrentStatus == OrderStatus.InProgress
            || order.CurrentStatus == OrderStatus.New)
        && (order.PaymentStatus == PaymentStatus.Paid
            || (order.PaymentType == PaymentType.Cash && order.RecurringTemplateId == null));

    /// <summary>
    /// In-memory form — the <c>TakeOrder</c> write gate. Same rule, C# semantics, and the SAME operand
    /// order, so the equivalence test stays a genuine pin rather than a coincidence.
    /// </summary>
    public static bool IsOfferable(
        OrderStatus currentStatus,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId) =>
        (currentStatus == OrderStatus.Confirmed
            || currentStatus == OrderStatus.OnTheWay
            || currentStatus == OrderStatus.InProgress
            || currentStatus == OrderStatus.New)
        && (paymentStatus == PaymentStatus.Paid
            || (paymentType == PaymentType.Cash && recurringTemplateId is null));
}
