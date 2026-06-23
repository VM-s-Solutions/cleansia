namespace Cleansia.Core.Domain.Auditing;

/// <summary>
/// ADR-0012 D2.2 — the out-of-band failure-audit writer. On a business-failure or a thrown exception the
/// action transaction never commits, so a row added to the scoped DbContext would be discarded with it.
/// The sink writes the <c>Success = false</c> row in its own short-lived, independently-committed scope.
/// It is best-effort and SWALLOWED by the behavior: a failure to record a failed action must never
/// convert into a different error returned to the admin (D2.2).
/// </summary>
public interface IAuditFailureSink
{
    Task RecordFailureAsync(AdminActionAudit entry, CancellationToken cancellationToken);
}
