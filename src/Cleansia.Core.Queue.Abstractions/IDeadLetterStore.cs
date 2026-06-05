namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// ADR-0002 D3 (F3) — the durable dead-letter store. A <c>&lt;queue&gt;-poison</c> consumer calls
/// <see cref="RecordAsync"/> to persist a poisoned message body (the recovery/replay source) before it
/// logs at Error (the alert) and acks. For the two fiscal queues (<c>generate-receipt</c>,
/// <c>generate-invoice</c>) writing the durable row is MANDATORY (AC3); the other three log+alert+store
/// at minimum.
///
/// <para>Lives alongside <see cref="IQueueClient"/> / <see cref="IPendingDispatch"/> as part of the
/// internal queue contract. The Wave-0 backing persists a <c>DeadLetter</c> row and OWNS ITS OWN
/// COMMIT — the poison consumer has no MediatR pipeline/UnitOfWork wrapping it.</para>
/// </summary>
public interface IDeadLetterStore
{
    /// <summary>
    /// Persists a durable dead-letter record for a poisoned message and commits it.
    /// </summary>
    /// <param name="sourceQueue">The business queue the message poisoned on (WITHOUT the
    /// <c>-poison</c> suffix), e.g. <c>generate-receipt</c> — one of <see cref="QueueNames"/>.</param>
    /// <param name="body">The raw, verbatim poisoned message body (stored unbounded).</param>
    /// <param name="error">Optional error/exception text when the consumer has it; otherwise
    /// <c>null</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordAsync(string sourceQueue, string body, string? error = null, CancellationToken ct = default);
}
