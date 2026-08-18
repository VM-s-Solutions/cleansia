using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IGdprDeletionService
{
    /// <summary>
    /// Runs the full GDPR Article 17 deletion cascade for a user: blocking-state checks,
    /// Stripe membership cancellation, blob removal, and entity anonymization.
    /// <paramref name="resolveAuditActor"/> is invoked AFTER the user is loaded so the
    /// caller can derive the audit (processedBy, notes) tuple from the user's email
    /// (customer self-delete) or an external admin identity (admin-initiated delete).
    /// </summary>
    /// <param name="deferEmployeeErasure">
    /// <c>true</c> — a subject who has an <c>Employee</c> is NOT erased; a Pending
    /// <c>GdprRequest</c> is filed and nothing else happens. This is the SELF path: ending a
    /// working relationship needs signed paperwork and an in-person step, so a cleaner cannot
    /// erase themselves out of a live engagement.
    /// <c>false</c> — the cascade runs for employees too. This is the ADMIN path, which is how a
    /// filed request is eventually fulfilled.
    /// <para>There is no default on purpose. The two paths call the same method, and a caller that
    /// does not state which one it is has not thought about it — the first version of this change
    /// forked inside the service on the subject alone, which silently made cleaners
    /// unerasable by anyone, admins included.</para>
    /// </param>
    Task<BusinessResult> DeleteUserAccountAsync(
        string userId,
        string deactivationReason,
        Func<User, (string ProcessedBy, string? Notes)> resolveAuditActor,
        bool deferEmployeeErasure,
        CancellationToken cancellationToken);
}
