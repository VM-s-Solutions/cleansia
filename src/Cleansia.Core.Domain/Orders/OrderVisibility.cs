using System.Linq.Expressions;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// ADR-0036 D5 — the ONE definition of "is this order open to this cleaner right now", asking only
/// about the preferred-cleaner hold. It is a different question from
/// <see cref="OrderAvailability"/>'s "is this order live work someone may take" (ADR-0037) and from a
/// cleaner's own slot conflict; each surface conjoins the ones it needs and none of them hand-rolls a
/// copy of this rule.
///
/// <para>An order is open to <c>employeeId</c> unless a preferred hold is live, belongs to someone
/// else, and has not yet been consumed:</para>
/// <list type="number">
///   <item>no deadline — every legacy row and every order without a granted hold</item>
///   <item>no beneficiary — an inconsistent pair fails OPEN rather than stranding the order</item>
///   <item>the deadline passed — the expiry, which needs no actor</item>
///   <item>the caller IS the beneficiary — the perk</item>
///   <item>the order already has a cleaner — first refusal is on the FIRST seat, not a lease</item>
/// </list>
///
/// <para>Terms 2 and 5 are what make a stuck-held order unreachable rather than merely unlikely. Term 2
/// is the read half of the pair whose write half is <see cref="Order.GrantPreferredHold"/>: the
/// aggregate refuses to write a deadline with no beneficiary, and if a row carries one anyway — a
/// manual UPDATE, a bad backfill, a future writer — this rule reads it as no hold. One end is not
/// enough, because a hold nobody may act on is a hold no actor is permitted to clear (D2.1 forbids the
/// sweep that would). Term 5 releases the remaining seats the instant ANY cleaner is on the order,
/// including one an admin assigned over the top of the perk.</para>
///
/// <para>The two forms are pinned against each other by TC-PREF-EQUIV-0 against real PostgreSQL, never
/// by review and never by sharing one expression tree: a null caller id is UNKNOWN under SQL's
/// three-valued logic and <c>true</c> under C# equality, so the same tree gives opposite answers.</para>
///
/// <para><b>The currency term</b> (owner ruling 2026-09-12: a cleaner is paid in the currency of the
/// country they work in) is the second (order, cleaner) question this type answers, as
/// <see cref="PayableTo(string?, string?)"/>. An order priced in another currency would produce a pay
/// row, and then an invoice, in a currency the cleaner's payout account does not hold — one
/// <c>ApproveInvoice</c> refuses. So it is not offered, not listed, not browsable and not takeable: the
/// board, the browse gate and the take gate read it through <see cref="OpenTo(string?, string?, DateTime)"/>,
/// and the pending-offer list — the hold's own beneficiary list, which needs no hold term — conjoins
/// <c>PayableTo</c> alone. An order the cleaner is ALREADY on stays open to them whatever its
/// currency: <c>AdminReassignOrder</c> is deliberately not gated (admin override), and an assignment
/// made over the top of this rule must still be visible to the cleaner it was made for.</para>
/// </summary>
public static class OrderVisibility
{
    /// <summary>
    /// The order is priced in the currency this cleaner is paid in, or the cleaner is already on it. A
    /// null currency is nobody's currency: the term fails CLOSED, so a caller that forgot to resolve
    /// one gets an empty board rather than every board.
    /// </summary>
    public static Expression<Func<Order, bool>> PayableTo(string? employeeId, string? cleanerCurrencyId)
        => o => o.CurrencyId == cleanerCurrencyId
             || o.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId);

    public static bool PayableTo(Order order, string? employeeId, string? cleanerCurrencyId)
        => (cleanerCurrencyId is not null && order.CurrencyId == cleanerCurrencyId)
        || (!string.IsNullOrEmpty(employeeId) && order.AssignedEmployees.Any(ae => ae.EmployeeId == employeeId));

    /// <summary>
    /// Both (order, cleaner) questions at once — the hold and the currency. The board, the take gate
    /// and the browse gate ask this one; <see cref="NotHeldFrom(string?, DateTime)"/> alone is for the
    /// paths that act on a hold without taking the order (declining it).
    /// </summary>
    public static Expression<Func<Order, bool>> OpenTo(string? employeeId, string? cleanerCurrencyId, DateTime nowUtc)
        => (new DirectSpecification<Order>(NotHeldFrom(employeeId, nowUtc))
            & new DirectSpecification<Order>(PayableTo(employeeId, cleanerCurrencyId)))
            .SatisfiedBy();

    public static bool OpenTo(Order order, string? employeeId, string? cleanerCurrencyId, DateTime nowUtc)
        => NotHeldFrom(order, employeeId, nowUtc) && PayableTo(order, employeeId, cleanerCurrencyId);

    public static Expression<Func<Order, bool>> NotHeldFrom(string? employeeId, DateTime nowUtc)
        => o => o.PreferredHoldUntilUtc == null
             || o.PreferredEmployeeId == null
             || o.PreferredHoldUntilUtc <= nowUtc
             || o.PreferredEmployeeId == employeeId
             || o.AssignedEmployees.Any();

    /// <summary>
    /// The same five terms for a materialized entity. Sole caller:
    /// <c>OrderAccessService.CanBrowseOrderAsync</c>, which covers order DETAIL only — order photos
    /// moved to the strict <c>CanAccessOrderAsync</c> gate on 2026-08-09, because a non-assignee was
    /// receiving a SAS URL per photograph of the customer's home. The detail it still admits is
    /// PII-redacted for anyone the order does not belong to (<c>OrderPiiRedaction</c>).
    /// </summary>
    public static bool NotHeldFrom(Order order, string? employeeId, DateTime nowUtc)
        => order.PreferredHoldUntilUtc == null
        || order.PreferredEmployeeId == null
        || order.PreferredHoldUntilUtc <= nowUtc
        || (!string.IsNullOrEmpty(employeeId) && order.PreferredEmployeeId == employeeId)
        || order.AssignedEmployees.Count > 0;
}
