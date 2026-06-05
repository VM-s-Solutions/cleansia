using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 / D3.1 (F3) — the shared body for every <c>&lt;queue&gt;-poison</c> consumer. A
/// poisoned message (one that exhausted <c>maxDequeueCount</c> = 5 on its business queue and was moved
/// by the Storage-queue runtime to <c>&lt;queue&gt;-poison</c>) is handled here with EXACTLY this
/// contract and nothing more:
///   1. <see cref="IDeadLetterStore.RecordAsync"/> — persist the durable, admin-visible dead-letter
///      row (the recovery/replay source). For the two fiscal queues (generate-receipt,
///      generate-invoice) this durable row is MANDATORY.
///   2. <c>LogError</c> — raises the Sentry/AppInsights alert.
///   3. ACK (return, <b>NEVER throw</b>) — acking removes the message from the <c>-poison</c> queue;
///      throwing would re-poison it into an endless loop. The durable row is the recovery source.
///
/// The poison consumer NEVER re-processes the original effect (no receipt/invoice/push/pay re-run) —
/// it is purely "persist + alert".
///
/// Lives in the testable Core library (the ADR-0002 D5 step 1 pattern); the
/// <c>[QueueTrigger("&lt;queue&gt;-poison")]</c> shells stay in the Exe so the Worker SDK source-gen
/// discovers them.
/// </summary>
public abstract class PoisonHandlerBase(IDeadLetterStore deadLetterStore, ILogger logger)
{
    /// <summary>The business queue this poison consumer is paired with (WITHOUT the <c>-poison</c>
    /// suffix), e.g. <c>generate-receipt</c> — one of <see cref="QueueNames"/>.</summary>
    protected abstract string SourceQueue { get; }

    public async Task HandleAsync(string body, CancellationToken ct)
    {
        // 1. Durable, admin-visible record (the recovery/replay source). The store owns its own commit.
        //    GUARD the persist so a transient DB fault does NOT throw out of this poison
        //    consumer. The base contract is "never throw / never loop"; an unguarded RecordAsync throw
        //    would fail to ACK and re-poison into an endless <queue>-poison-poison loop. On a persistent
        //    DB failure we still raise the alert (the body is in the Error log) and ACK — accepting the
        //    rare lost durable row as the lesser evil vs an infinite poison loop.
        try
        {
            await deadLetterStore.RecordAsync(SourceQueue, body, error: null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "DEAD-LETTER PERSIST FAILED on {SourceQueue} — alerting and acking to avoid a poison loop. "
                + "The message body is logged below for manual recovery: {Body}",
                SourceQueue, body);
            return;
        }

        // 2. Alert. LogError raises the Sentry/AppInsights alert so a poisoned (especially fiscal)
        //    message is noticed, not silently lost.
        logger.LogError("DEAD-LETTER on {SourceQueue}: {Body}", SourceQueue, body);

        // 3. ACK — return without throwing. The Storage-queue runtime deletes the message from
        //    <queue>-poison; the DeadLetter row above is the durable recovery source. NEVER throw here:
        //    a throw would re-poison and loop forever.
    }
}
