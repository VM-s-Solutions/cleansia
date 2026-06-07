using Cleansia.Core.Domain.Common;

namespace Cleansia.Core.Domain.Outbox;

/// <summary>
/// The durable record of one intended post-commit queue send. A row is written into the same scoped
/// unit of work the pipeline commits, so a message exists if and only if the business state committed,
/// and a single drainer later puts the body on the wire at-least-once.
/// </summary>
public class OutboxMessage : Auditable, ITenantEntity
{
    public string QueueName { get; private set; } = default!;

    public string MessageKey { get; private set; } = default!;

    /// <summary>The already-serialized wire body, stored verbatim and sent unchanged by the drainer.</summary>
    public string Body { get; private set; } = default!;

    public OutboxMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? ClaimedOn { get; private set; }

    /// <summary>The drainer instance / lease token holding the row.</summary>
    public string? ClaimedBy { get; private set; }

    public DateTimeOffset? DispatchedOn { get; private set; }

    /// <summary>Earliest time the drainer may re-attempt a failed row; null means immediately eligible.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(string queueName, string messageKey, string body, string? tenantId)
        => new()
        {
            QueueName = queueName,
            MessageKey = messageKey,
            Body = body,
            TenantId = tenantId,
            Status = OutboxMessageStatus.Pending,
        };

    public void Claim(string claimToken, DateTimeOffset claimedOn)
    {
        ClaimedBy = claimToken;
        ClaimedOn = claimedOn;
    }

    public void MarkDispatched(DateTimeOffset dispatchedOn)
    {
        Status = OutboxMessageStatus.Dispatched;
        DispatchedOn = dispatchedOn;
        ClaimedBy = null;
        ClaimedOn = null;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <summary>
    /// Releases the lease so the row is claimable again, schedules the next attempt past
    /// <paramref name="nextAttemptAt"/>, and records the failure. A send that fails this way is
    /// retried; it is not lost.
    /// </summary>
    public void Reschedule(DateTimeOffset nextAttemptAt, string? error)
    {
        AttemptCount++;
        Status = OutboxMessageStatus.Pending;
        ClaimedBy = null;
        ClaimedOn = null;
        NextAttemptAt = nextAttemptAt;
        LastError = error;
    }

    /// <summary>
    /// Retires a row that has exhausted its retry budget: it stops being claimable and is surfaced
    /// through the dead-letter path instead of silently looping forever.
    /// </summary>
    public void MarkFailed(string? error)
    {
        AttemptCount++;
        Status = OutboxMessageStatus.Failed;
        ClaimedBy = null;
        ClaimedOn = null;
        NextAttemptAt = null;
        LastError = error;
    }
}
