using System.ComponentModel.DataAnnotations;
using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Auditing;

/// <summary>
/// What a cleaner did to a job, kept because the act erases its own evidence.
///
/// <para><b>Its own table, never <see cref="AdminActionAudit"/>.</b> Owner ruling 2026-09-06: admins
/// and employees are kept apart. That table's own erasure verdict asserts "its ActorEmail is the ADMIN
/// who acted", and writing cleaner rows into it would make the sentence false while mixing them into
/// the admin audit-log screen.</para>
///
/// <para><b>Nothing in the ADR-0012 pipeline can write this row.</b> <c>AdminMutationGate</c> requires
/// the Administrator role claim, so a partner-app command produces no audit row at all. Rows here are
/// added explicitly by a handler through <see cref="Repositories.IEmployeeActionAuditRepository"/>,
/// which rides the UnitOfWork's single commit. Do not wire this into <c>AuditLogBehavior</c>.</para>
///
/// <para><b>Why it exists.</b> <c>Order.UnassignEmployee</c> hard-deletes the <c>OrderEmployee</c> row
/// — load-bearing, because the unique seat index is unfiltered and a released ordinal frees itself by
/// disappearing. <c>OrderEmployeePay</c> is only written for COMPLETED orders. So after a cleaner
/// leaves a job, no row anywhere says they were ever on it.</para>
///
/// <para>Append-only: private setters, one factory, no mutators, and nothing prunes it. Deliberately
/// three declared columns — there is no reason field, no before/after JSON and no actor profile,
/// because nothing reads them and ADR-0045 D13 refuses collection just in case. <c>CreatedOn</c> IS
/// the occurred-at and <c>CreatedBy</c> IS the acting principal, both stamped at commit.</para>
/// </summary>
public class EmployeeActionAudit : Auditable, ITenantEntity
{
    /// <summary>
    /// The cleaner who acted. A bare scalar — no navigation and no FK, because the row must outlive
    /// everything it names, and the assignment it describes is deleted by the act itself.
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string EmployeeId { get; private set; } = default!;

    /// <summary>
    /// The job. Without it the row cannot reach the lead time, the price or the credit movement, and
    /// every question this table exists to answer dies with it.
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string OrderId { get; private set; } = default!;

    [Required]
    public EmployeeAuditAction Action { get; private set; }

    private EmployeeActionAudit()
    {
    }

    /// <summary>
    /// The only way to make one. <c>CreatedBy</c>, <c>CreatedOn</c> and <c>TenantId</c> are NOT set
    /// here — <c>CleansiaDbContext.CommitAsync</c> stamps all three, which is the whole reason this is
    /// <see cref="Auditable"/> rather than <c>BaseEntity</c> like its admin-side sibling.
    /// </summary>
    public static EmployeeActionAudit Create(
        string employeeId, string orderId, EmployeeAuditAction action) =>
        new()
        {
            EmployeeId = employeeId,
            OrderId = orderId,
            Action = action,
        };
}
