using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Services;

public sealed class OutboxDrainerService(
    IOutboxMessageRepository outboxRepository,
    IQueueClient queueClient,
    IDeadLetterStore deadLetterStore,
    ITenantProvider tenantProvider,
    IOutboxDrainerConfig config,
    ILogger<OutboxDrainerService> logger)
    : IOutboxDrainerService
{
    private static readonly string InstanceId = Environment.MachineName;

    public async Task<int> DrainOnceAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var leaseCutoff = now.AddSeconds(-config.LeaseSeconds);
        var claimToken = $"{InstanceId}:{Guid.NewGuid():N}";

        var claimed = await outboxRepository.ClaimPendingBatchAsync(
            claimToken, config.BatchSize, now, leaseCutoff, cancellationToken);
        if (claimed.Count == 0)
        {
            return 0;
        }

        // Persist the claim before any send so a crash mid-batch leaves the rows leased, not lost; the
        // lease + the future NextAttemptAt is what makes a re-claim safe.
        await outboxRepository.CommitAsync(cancellationToken);

        var dispatched = 0;
        foreach (var row in claimed)
        {
            if (await TrySendAsync(row, cancellationToken))
            {
                dispatched++;
            }
        }

        await outboxRepository.CommitAsync(cancellationToken);
        return dispatched;
    }

    private async Task<bool> TrySendAsync(OutboxMessage row, CancellationToken cancellationToken)
    {
        tenantProvider.ClearTenantOverride();
        if (!string.IsNullOrEmpty(row.TenantId))
        {
            tenantProvider.SetTenantOverride(row.TenantId);
        }

        try
        {
            // Body is the already-serialized QueueEnvelope<T>; the client forwards a string verbatim.
            await queueClient.SendAsync(row.QueueName, row.Body, cancellationToken);
            row.MarkDispatched(DateTimeOffset.UtcNow);
            return true;
        }
        catch (Exception ex)
        {
            await HandleSendFailureAsync(row, ex, cancellationToken);
            return false;
        }
        finally
        {
            tenantProvider.ClearTenantOverride();
        }
    }

    private async Task HandleSendFailureAsync(OutboxMessage row, Exception ex, CancellationToken cancellationToken)
    {
        if (row.AttemptCount + 1 >= config.MaxAttempts)
        {
            row.MarkFailed(ex.Message);
            logger.LogError(
                ex,
                "Outbox row {OutboxId} for queue {Queue} (key {MessageKey}) exhausted its retry budget; dead-lettering.",
                row.Id, row.QueueName, row.MessageKey);
            await deadLetterStore.RecordAsync(row.QueueName, row.Body, ex.Message, cancellationToken);
            return;
        }

        var nextAttemptAt = DateTimeOffset.UtcNow.Add(Backoff(row.AttemptCount));
        row.Reschedule(nextAttemptAt, ex.Message);
        logger.LogWarning(
            ex,
            "Outbox row {OutboxId} for queue {Queue} (key {MessageKey}) failed to send; retry at {NextAttemptAt}.",
            row.Id, row.QueueName, row.MessageKey, nextAttemptAt);
    }

    private TimeSpan Backoff(int priorAttempts)
    {
        var seconds = config.BaseBackoffSeconds * Math.Pow(2, priorAttempts);
        var jitter = Random.Shared.NextDouble() * config.BaseBackoffSeconds;
        return TimeSpan.FromSeconds(seconds + jitter);
    }
}
