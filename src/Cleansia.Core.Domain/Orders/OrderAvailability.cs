using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// The shared offerability rule: fulfilment and payment qualification while the raw booked slot is
/// still open. SQL and in-memory forms receive the same caller-supplied UTC instant and are pinned
/// against PostgreSQL. Seat capacity and cleaner visibility remain separate. → /domain/offerability
/// </summary>
public static class OrderAvailability
{
    /// <summary>
    /// The COARSE fulfilment-axis floor — the statuses the rule can ever admit. NOT the rule: every
    /// member is conditional on the money term. It exists because the clients cannot evaluate the money
    /// term (they filter on none of the money columns) and because it is the index-served prefilter on
    /// <c>Orders.CurrentStatus</c>.
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
    public static Expression<Func<Order, bool>> IsOfferableSql(DateTime nowUtc) => order =>
        (order.CurrentStatus == OrderStatus.Confirmed
            || order.CurrentStatus == OrderStatus.OnTheWay
            || order.CurrentStatus == OrderStatus.InProgress
            || order.CurrentStatus == OrderStatus.New)
        && (order.PaymentStatus == PaymentStatus.Paid
            || (order.PaymentType == PaymentType.Cash
                && (order.RecurringTemplateId == null || order.CustomerConfirmedAt != null)))
        && order.CleaningDateTime.AddMinutes(order.EstimatedTime) > nowUtc;

    /// <summary>
    /// In-memory form — the <c>TakeOrder</c> write gate. Same rule, C# semantics, and the SAME operand
    /// order, so the equivalence test stays a genuine pin rather than a coincidence.
    /// </summary>
    public static bool IsOfferable(
        OrderStatus currentStatus,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId,
        DateTime? customerConfirmedAt,
        DateTime cleaningDateTime,
        int estimatedTimeMinutes,
        DateTime nowUtc) =>
        (currentStatus == OrderStatus.Confirmed
            || currentStatus == OrderStatus.OnTheWay
            || currentStatus == OrderStatus.InProgress
            || currentStatus == OrderStatus.New)
        && (paymentStatus == PaymentStatus.Paid
            || (paymentType == PaymentType.Cash
                && (recurringTemplateId is null || customerConfirmedAt is not null)))
        && cleaningDateTime.AddMinutes(estimatedTimeMinutes) > nowUtc;

    public static bool IsOfferable(Order order, DateTime nowUtc) => IsOfferable(
        order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId, order.CustomerConfirmedAt,
        order.CleaningDateTime, order.EstimatedTime, nowUtc);
}
