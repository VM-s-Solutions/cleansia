namespace Cleansia.Core.Domain.Auditing;

/// <summary>
/// What a cleaner did to an assignment they held.
///
/// <para><b>Only members with a real writer belong here.</b> ADR-0045 D13 refuses data collected just
/// in case, and an enum member nobody writes is a claim the platform records something it does not.
/// The two below are the two commands that exist.</para>
///
/// <para>The two OTHER callers of <c>Order.UnassignEmployee</c> — <c>AdminReassignOrder</c> and
/// <c>RejectEmployee</c> — are ADMIN actions and already produce an <c>AdminActionAudit</c> row. They
/// deliberately do not write here: owner ruling 2026-09-06 keeps the two tables apart, and an admin
/// releasing a seat is not the cleaner's own act.</para>
/// </summary>
public enum EmployeeAuditAction
{
    /// <summary>
    /// The cleaner asked for someone to take the job off them, and stayed on the hook until one did.
    /// → <c>RequestCover</c>
    /// </summary>
    CoverRequested = 1,

    /// <summary>
    /// The cleaner released their own seat outright. The booking is NOT cancelled — the seat returns
    /// to the board and the job runs with whoever is left (owner ruling 2026-09-06). → <c>DropOrder</c>
    /// </summary>
    OrderDropped = 2,
}
