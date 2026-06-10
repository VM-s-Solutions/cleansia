using Cleansia.Core.Domain.Messaging;

namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// Insert-claim persistence for <see cref="ProcessedMessage"/>. Used by the durable idempotency guard
/// to CLAIM a deterministic consumer message key in its OWN transaction before the terminal effect.
/// Mirrors <see cref="IProcessedStripeEventRepository"/>.
/// </summary>
public interface IProcessedMessageRepository : IRepository<ProcessedMessage, string>
{
}
