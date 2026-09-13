using Cleansia.Core.Domain.Auditing;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Adds, reads, one erasure write and one retention delete — nothing that updates or removes a row
/// by hand. The inherited <c>Remove</c>/<c>Deactivate</c> members exist on every repository and are
/// pinned unused on this one by <c>CustomerActionAuditImmutabilityTests</c>. → <see cref="CustomerActionAudit"/>
/// </summary>
public interface ICustomerActionAuditRepository : IRepository<CustomerActionAudit, string>
{
    /// <summary>
    /// Loads the subject's rows TRACKED and calls <see cref="CustomerActionAudit.Pseudonymise"/> on each,
    /// so the blanking rides the erasure's single commit and a commit failure leaves the trail intact.
    /// Returns the number of rows touched.
    /// </summary>
    Task<int> PseudonymiseForSubjectAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every row whose own <see cref="CustomerActionAudit.OccurredOn"/> is before the cutoff,
    /// across tenants, <paramref name="batchSize"/> rows per statement until none is left. Returns the
    /// number of rows deleted.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);
}
