using Cleansia.Core.Domain.Outbox;

namespace Cleansia.Core.Domain.Repositories;

public interface IOutboxMessageRepository : IRepository<OutboxMessage, string>
{
    /// <summary>
    /// Atomically claims a batch of due, undispatched rows under <paramref name="claimToken"/> and
    /// returns them tracked. A row is eligible when it is still pending, its retry backoff has elapsed
    /// (<c>NextAttemptAt</c> null or past <paramref name="now"/>), and it is not held under a live lease
    /// (<c>ClaimedOn</c> null or at/before <paramref name="leaseCutoff"/>). The claim uses a row-level
    /// lock that skips already-locked rows, so a second drainer running at the same time never grabs the
    /// same row, and the lease cutoff is what makes a crashed claim re-claimable only after it expires.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingBatchAsync(
        string claimToken,
        int batchSize,
        DateTimeOffset now,
        DateTimeOffset leaseCutoff,
        CancellationToken cancellationToken);

    /// <summary>
    /// The claim-before-act fast path on the frozen <c>(QueueName, MessageKey)</c> identity: returns the
    /// existing row for a deterministic key, or null. A producer enqueuing an effect that must run at
    /// most once per key (a sitewide-promo campaign) reads this first and short-circuits when a row
    /// already exists, with the unique index as the concurrent backstop. System-scoped — ignores the
    /// tenant filter, since the key already embeds the tenant.
    /// </summary>
    Task<OutboxMessage?> GetByQueueAndKeyAsync(
        string queueName,
        string messageKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// GDPR erasure. Deletes the subject's <c>send-email</c> rows — the one queue whose
    /// <see cref="OutboxMessage.Body"/> carries an address and a real name in the clear, stored verbatim as
    /// the wire payload. Id-keyed and set-based because an <see cref="OutboxMessage"/> has no navigation to a
    /// user at all, so there is nothing an erasure could reach through (the shape
    /// <see cref="IEmployeePayoutDetailsRepository.RemoveForEmployeeAsync"/> already uses).
    ///
    /// <para><b>Every status, deliberately.</b> No retention path bounds the rows that matter: the prune
    /// deletes only <c>Dispatched</c> rows past its window, and refuses <c>Pending</c>/<c>Failed</c> outright
    /// because they are still re-drivable. Whether a retry-exhausted row ever earns an ageing clock is
    /// ADR-0002 §A8's question, not this call's — erasure of an identified subject is unconditional either
    /// way, and re-driving a send-email for an account that has just been erased and deactivated would
    /// re-issue the confirmation/reset mail to the address the platform said it had deleted.</para>
    ///
    /// <para>The subject is read STRUCTURALLY out of the <see cref="OutboxMessage.MessageKey"/> COLUMN — the
    /// frozen <c>email:{purpose}:{userId}:{codeHash}</c> formula — never as a substring of the body. The
    /// push, receipt, pay and invoice rows carry no address or name, and a push key repeats the same user id
    /// in its own first segment, so they are exactly the rows a looser match would wrongly take: they
    /// stay.</para>
    ///
    /// <para><b>The boundary this cannot cross.</b> The key column is the only handle here; the body is never
    /// read. A <c>send-email</c> row whose key was not built by the frozen formula therefore stays, address
    /// and all. Nothing writes one today — <c>EmailDispatch</c> is the only producer of a send-email
    /// envelope — so this is a limit of the handle, not a live hole, and it is the sentence to re-check the
    /// day a second producer appears. Out of reach here by construction, and not by widening this match: the
    /// same body already on the queue once the drainer has sent it (bounded by the queue's own lifetime) and
    /// its poisoned copy in <c>DeadLetter</c> (taken by that table's own erasure).</para>
    /// </summary>
    Task RemoveForSubjectAsync(string userId, CancellationToken cancellationToken);
}
