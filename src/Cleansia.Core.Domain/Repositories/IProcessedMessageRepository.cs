using Cleansia.Core.Domain.Messaging;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Insert-claim persistence for <see cref="ProcessedMessage"/>. Used by the durable idempotency guard
/// to CLAIM a deterministic consumer message key in its OWN transaction before the terminal effect.
/// Mirrors <see cref="IProcessedStripeEventRepository"/>.
/// </summary>
public interface IProcessedMessageRepository : IRepository<ProcessedMessage, string>
{
    /// <summary>
    /// True when a claim row already exists for the given message key. The guard checks this before the
    /// insert so the common redelivery path short-circuits WITHOUT attempting (and logging) a failing
    /// duplicate insert; the unique-index insert-catch remains the race-safe fallback for the rare
    /// two-parallel-redeliveries case (both miss this check, one insert wins, the other gets 23505).
    /// </summary>
    Task<bool> HasProcessedAsync(string messageKey, CancellationToken cancellationToken);
}
