using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 — the shared body for every <c>&lt;queue&gt;-poison</c> consumer: persist a durable
/// dead-letter row (body verbatim), alert, then <b>ACK — return, NEVER throw</b>, because throwing
/// re-poisons the message into an endless loop. <b>It never re-runs the original effect</b>; the row is
/// the recovery source.
///
/// <para><b>The alert carries the message's IDENTITY and never its body.</b> One of the bodies reaching
/// here is the outbound email, whose code is a live confirmation/reset token; the alert's sinks include
/// a vendor where structured values become indexed tags. When persisting fails we still alert and still
/// ACK, and the alert is deliberately NOT widened to carry the body as a last copy.
/// → /flows/cross-cutting#dead-letters</para>
///
/// <para>Lives in the testable Core library; the <c>[QueueTrigger]</c> shells stay in the Exe so the
/// Worker SDK source-gen discovers them.</para>
/// </summary>
public abstract class PoisonHandlerBase(IDeadLetterStore deadLetterStore, ILogger logger)
{
    /// <summary>The business queue this poison consumer is paired with (WITHOUT the <c>-poison</c>
    /// suffix), e.g. <c>generate-receipt</c> — one of <see cref="QueueNames"/>.</summary>
    protected abstract string SourceQueue { get; }

    public async Task HandleAsync(string body, CancellationToken ct)
    {
        // What the ALERT is allowed to say. The body itself goes to the durable row below and nowhere
        // else — see the class remarks.
        var alert = PoisonAlert.Describe(body);

        // 1. Durable, admin-visible record (the recovery/replay source), stored VERBATIM. The store owns
        //    its own commit.
        //    GUARD the persist so a transient DB fault does NOT throw out of this poison
        //    consumer. The base contract is "never throw / never loop"; an unguarded RecordAsync throw
        //    would fail to ACK and re-poison into an endless <queue>-poison-poison loop. On a persistent
        //    DB failure we still raise the alert and ACK — accepting the rare lost durable row as the
        //    lesser evil vs an infinite poison loop.
        try
        {
            await deadLetterStore.RecordAsync(SourceQueue, body, error: null, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "DEAD-LETTER PERSIST FAILED on {SourceQueue} (key {MessageKey}, tenant {TenantId}, "
                + "{Bytes} bytes, body fingerprint {Fingerprint}) — alerting and acking to avoid a poison "
                + "loop. THIS MESSAGE HAS NO DURABLE ROW: the body is deliberately not reproduced here "
                + "(it can carry a live reset token) and is not recoverable from this alert.",
                SourceQueue, alert.MessageKey, alert.TenantId, alert.Bytes, alert.Fingerprint);
            return;
        }

        // 2. Alert. LogError raises the Sentry alert so a poisoned (especially fiscal) message is
        //    noticed, not silently lost. Identity only — the verbatim body is one row lookup away:
        //    SELECT * FROM "DeadLetters" WHERE "SourceQueue" = <SourceQueue> AND "RawBody" LIKE '%<MessageKey>%'
        logger.LogError(
            "DEAD-LETTER on {SourceQueue} (key {MessageKey}, tenant {TenantId}, {Bytes} bytes, body "
            + "fingerprint {Fingerprint}) — the verbatim body is in the DeadLetter row for this queue; "
            + "it is deliberately not in this alert.",
            SourceQueue, alert.MessageKey, alert.TenantId, alert.Bytes, alert.Fingerprint);

        // 3. ACK — return without throwing. The Storage-queue runtime deletes the message from
        //    <queue>-poison; the DeadLetter row above is the durable recovery source. NEVER throw here:
        //    a throw would re-poison and loop forever.
    }
}
