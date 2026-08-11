using System.Linq.Expressions;

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
/// </summary>
public static class OrderVisibility
{
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
