using Cleansia.Core.Domain.Auditing;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Adds, reads, two erasure writes and one retention delete — nothing that updates or removes a row
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
    /// The subject's GUEST rows: acts with no <c>UserId</c> against one of the subject's orders — a booking
    /// placed with the account's e-mail before, or instead of, signing in (<c>SubjectOrders</c>). Reached
    /// by the order because nothing else links them. Same tracked walk, same single commit. Returns the
    /// number of rows touched.
    /// </summary>
    Task<int> PseudonymiseGuestRowsForOrdersAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every row of the AMBIENT operating company whose own
    /// <see cref="CustomerActionAudit.OccurredOn"/> is before the cutoff, in batches. Each company keeps
    /// its own window, so the retention job calls this once per company under that company's override.
    /// Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
}
