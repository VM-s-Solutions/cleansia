using Cleansia.Core.Domain.Auditing;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Adds, the base reads, one out-of-band write and the retention delete. The one reader is the admin
/// timeline (ADR-0062 D6), which lists a cleaner's acts beside the customer's and the admin's for one order
/// through <c>GetQueryable()</c>. → <see cref="EmployeeActionAudit"/>
/// </summary>
public interface IEmployeeActionAuditRepository : IRepository<EmployeeActionAudit, string>
{
    /// <summary>
    /// Writes <paramref name="entry"/> unless a row for the same cleaner, order and act already exists.
    /// SELF-COMMITTING, in a DbContext of its own — a declared exception, for an act recorded from a read:
    /// a query has no commit to ride, and committing the request's own context would flush whatever else it
    /// tracks. The caller stamps the tenant.
    /// </summary>
    Task RecordOnceOutOfBandAsync(EmployeeActionAudit entry, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every row of the AMBIENT operating company created before the cutoff, in batches. The
    /// retention job calls this once per company under that company's override. Returns the number deleted.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
