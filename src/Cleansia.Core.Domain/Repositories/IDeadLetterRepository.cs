using Cleansia.Core.Domain.DeadLettering;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Persistence for <see cref="DeadLetter"/> — the durable dead-letter rows written by the
/// <c>&lt;queue&gt;-poison</c> consumers (ADR-0002 D3 / F3).
///
/// There is NO reader. No query, no admin endpoint and no replay command loads one of these rows; the
/// only path that touches a row after the write is GDPR erasure, below, which deletes it. A row is the
/// record that a thing failed, never the mechanism for making it succeed.
/// → /domain/roles/dead-letter-record
/// </summary>
public interface IDeadLetterRepository : IRepository<DeadLetter, string>
{
    /// <summary>
    /// GDPR erasure. Deletes the subject's poisoned <c>send-email</c> rows — the one queue body that
    /// carries an address and a real name in the clear. Id-keyed and set-based because a
    /// <see cref="DeadLetter"/> has no navigation to a user at all, so there is nothing an erasure could
    /// reach through (the shape <see cref="IEmployeePayoutDetailsRepository.RemoveForEmployeeAsync"/>
    /// already uses).
    ///
    /// <para><b>Deleted, not redacted in place.</b> ADR-0002 D3 calls this row "the recovery source", but
    /// recovering a poisoned send-email is a RE-ISSUE of the confirmation/reset mail — impossible, and
    /// undesirable, for an account that has just been erased and deactivated. The row's recovery value for
    /// this subject is therefore zero rather than merely reduced, D3 calls the durable row MANDATORY only
    /// for the two fiscal queues (neither touched here), and no code re-derives fiscal work from this
    /// table. A blanked body would leave a row still presenting itself as the verbatim recovery source
    /// while holding nothing recoverable.</para>
    ///
    /// <para>The subject is read STRUCTURALLY — the envelope's <c>MessageKey</c> user segment
    /// (<c>email:{purpose}:{userId}:{codeHash}</c>) or the payload's own <c>UserId</c> — never as a
    /// substring of the body. The push and live-activity bodies contain the same id and are the rows a
    /// substring match would wrongly take: they carry no identifier of their own and stay.</para>
    ///
    /// <para><b>The boundary this cannot cross.</b> A truncated body whose key survived the cut is still
    /// this subject's unambiguously and is still taken. What remains out of reach is the residue with no
    /// identity token left in it at all — the case the alert path reports as <c>&lt;unparseable&gt;</c>.
    /// No subject handle exists for it here or anywhere else, so no widening of this match would find it;
    /// it is bounded by the absolute-clock retention sweep instead, not by this call.</para>
    ///
    /// <para>One method rather than a predicate at the call site, because the handle is expected to move:
    /// today the subject lives inside <c>RawBody</c>, and promoting it to a column changes this body and
    /// nothing else.</para>
    /// </summary>
    Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken);
}
