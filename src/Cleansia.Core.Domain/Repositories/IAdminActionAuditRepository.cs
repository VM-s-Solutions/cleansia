using Cleansia.Core.Domain.Auditing;

namespace Cleansia.Core.Domain.Repositories;

public interface IAdminActionAuditRepository : IRepository<AdminActionAudit, string>
{
    /// <summary>
    /// Deletes every row of the AMBIENT operating company whose <see cref="AdminActionAudit.OccurredOn"/> is
    /// before the cutoff, in batches. The retention job calls this once per company under that company's
    /// override. Returns the number deleted.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
