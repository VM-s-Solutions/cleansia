using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.CompanyLifecycle;

/// <summary>
/// One sweep message on the company's own queue, drained after the commit so a refused request
/// enqueues nothing. Shared by the request and by <see cref="DeactivateCompany"/>, which re-runs the
/// sweep the moment the door closes.
/// </summary>
public static class CompanyWindDownDispatch
{
    /// <summary>
    /// Returns false when a message under the same key is already on the outbox. The key is the
    /// request instant to the second, so two acts within one second — a wind-down request and the
    /// deactivation right behind it, or a double-clicked re-run — ask for one run, and the row that
    /// is already there carries it; the unique (queue, key) index is the concurrent backstop.
    /// </summary>
    public static async Task<bool> EnqueueAsync(
        IPendingDispatch pendingDispatch,
        IOutboxMessageRepository outboxMessageRepository,
        string tenantId,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var key = MessageKeys.CompanyWindDown(tenantId, requestedAt);
        if (await outboxMessageRepository.GetByQueueAndKeyAsync(QueueNames.CompanyWindDown, key, cancellationToken) is not null)
        {
            return false;
        }

        pendingDispatch.Enqueue(
            QueueNames.CompanyWindDown,
            new QueueEnvelope<CompanyWindDownMessage>(key, tenantId, new CompanyWindDownMessage(tenantId)),
            key);
        return true;
    }
}
