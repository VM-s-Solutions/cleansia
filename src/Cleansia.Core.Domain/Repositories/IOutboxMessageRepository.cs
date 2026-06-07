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
}
