namespace Cleansia.Core.AppServices.Auditing;

/// <summary>
/// ADR-0012 D4 — the scoped per-request buffer a sensitive handler writes a typed, pre-redacted
/// before/after snapshot into, and <c>AuditLogBehavior</c> drains when it writes the success row.
/// Mirrors the <c>IPendingDispatch</c> seam (ADR-0002 D1): registered SCOPED, the producer is the
/// handler, the consumer is the behavior. The behavior never computes a diff — it only drains what the
/// handler emitted.
/// </summary>
public interface IAuditContext
{
    void RecordChange(string resourceType, string resourceId, object before, object after, string? reason = null);

    /// <summary>
    /// ADR-0062 D1 — the customer-side twin of <see cref="RecordChange"/>: one typed evidence record, no
    /// diff, no reason. <paramref name="actorUserId"/> exists for the acts whose actor does not exist
    /// until the handler creates them (registration): the session has no user id, the row must carry
    /// the new one. When the session does carry one, the session wins.
    /// </summary>
    void RecordEvidence(string resourceType, string resourceId, object payload, string? actorUserId = null);

    AuditSnapshot? DrainSnapshot();

    /// <summary>
    /// Per-request latch shared by the inner <c>AuditLogBehavior</c> and the outer
    /// <c>AuditFailureCaptureBehavior</c> so a failed action is recorded out-of-band exactly once.
    /// The inner behavior owns the failures it can see (a business failure the handler returned); the
    /// outer behavior owns the two it structurally cannot (a validation reject short-circuited outer to
    /// the inner behavior, and a commit-throw raised after the inner behavior already returned). Whichever
    /// records the failure latches it so the other skips.
    /// </summary>
    bool TryClaimFailureRecording();
}
