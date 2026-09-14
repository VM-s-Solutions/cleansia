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
    /// until the handler creates them (registration), or exists but is unnamed by the anonymous session
    /// (the session acts: a sign-in, a reset, a confirmation): the session has no user id, the row must
    /// carry the one the handler resolved. When the session does carry one, the session wins.
    /// <paramref name="resourceId"/> is
    /// null for an act whose aggregate does not exist yet (a membership checkout session: the webhook
    /// provisions the row) — the evidence is still worth more than the request's own ids.
    /// <paramref name="payload"/> is null for an act whose only evidence is that it happened, and to
    /// whom (a completed password reset): the row still needs the subject and resource the anonymous
    /// session cannot name. A validator that resolves the account it is about to refuse names it the same
    /// way, with no payload: a refusal on a KNOWN account is that account's row, so an account-takeover
    /// trail is keyed on the victim rather than reconstructed by IP. The failure arms read the subject
    /// and the resource off the snapshot and never its payload.
    /// </summary>
    void RecordEvidence(string resourceType, string? resourceId, object? payload, string? actorUserId = null);

    AuditSnapshot? DrainSnapshot();

    /// <summary>
    /// A marked command whose handler took a branch the marker does not describe declines the success
    /// row: a social sign-in that provisioned the account instead of opening a session is a
    /// registration, whose proof is the consent rows it writes, not a <c>customer.session.login</c>. A
    /// refusal on that branch is still recorded — the failure arms do not read this.
    /// </summary>
    void DeclineSuccessRow();

    bool SuccessRowDeclined { get; }

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
