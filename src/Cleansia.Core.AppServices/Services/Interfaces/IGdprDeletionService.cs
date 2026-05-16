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
    Task<BusinessResult> DeleteUserAccountAsync(
        string userId,
        string deactivationReason,
        Func<User, (string ProcessedBy, string? Notes)> resolveAuditActor,
        CancellationToken cancellationToken);
}
