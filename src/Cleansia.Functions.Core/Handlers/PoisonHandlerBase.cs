using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 / D3.1 (F3) — the shared body for every <c>&lt;queue&gt;-poison</c> consumer. A
/// poisoned message (one that exhausted <c>maxDequeueCount</c> = 5 on its business queue and was moved
/// by the Storage-queue runtime to <c>&lt;queue&gt;-poison</c>) is handled here with EXACTLY this
/// contract and nothing more:
///   1. <see cref="IDeadLetterStore.RecordAsync"/> — persist the durable, admin-visible dead-letter
///      row (the recovery/replay source), with the body VERBATIM. For the two fiscal queues
///      (generate-receipt, generate-invoice) this durable row is MANDATORY.
///   2. <c>LogError</c> — raises the Sentry alert, carrying the message's IDENTITY and never its body.
///      An Error log is what the Functions worker's Sentry logging provider
///      (<c>AddSentryMonitoring</c>) turns into an event, and that is this host's ONLY off-box error
///      signal: Application Insights was removed from every environment.
///   3. ACK (return, <b>NEVER throw</b>) — acking removes the message from the <c>-poison</c> queue;
///      throwing would re-poison it into an endless loop. The durable row is the recovery source.
///
/// The poison consumer NEVER re-processes the original effect (no receipt/invoice/push/pay re-run) —
/// it is purely "persist + alert".
///
/// <para><b>Why step 2 does not carry the body (S6).</b> One of the seven bodies that reach here is
/// <c>SendEmailMessage</c>, whose <c>Code</c> is the RAW confirmation/reset token — a live credential
/// that grants account takeover until it is consumed or expires. Step 1's sink is our own Postgres;
/// step 2's sinks are the Functions host's retained log stream and Sentry, a separate vendor with its
/// own retention, where the structured values additionally become INDEXED TAGS and a scope BREADCRUMB
/// that re-attaches to later, unrelated events. The two live consumers on this same worker already hold
/// exactly this line — <c>SendEmailHandler.cs:44</c> ("Never log the payload — it carries the recipient
/// email and a live confirmation/reset code") and <c>SendLiveActivityUpdateHandler.cs:45</c> ("never log
/// the raw body … log only its size") — and <c>MessageKeys.HashCode</c> exists so the send-email key
/// carries the token's hash "so the secret never appears in a key or a log line". This handler was the
/// one place on the queue path that did not. <see cref="PoisonAlert"/> is that line, made shared.</para>
///
/// <para><b>The persist-failed branch, decided rather than inherited.</b> When step 1 throws we still
/// alert and still ACK (never re-poison), so that message ends with NO durable row — and the alert is
/// deliberately NOT widened to carry the body as a last-copy substitute. Three reasons, in order of
/// weight:
/// (a) D3 never assigned the log a recovery role — it names the <c>DeadLetter</c> row as "the recovery
///     source" and the log as the alert, so withholding the body here changes no ADR contract;
/// (b) the queues whose durable row D3 calls MANDATORY lose <b>nothing</b> to this rule:
///     <c>GenerateReceiptMessage(OrderId, LanguageCode)</c> and
///     <c>GenerateInvoiceMessage(EmployeeId, PayPeriodId, LanguageCode)</c> contain no credential and no
///     PII, and their whole domain subject is already inside the <c>MessageKey</c> the alert carries, so
///     the fiscal persist-failed alert is materially unchanged — and D3.4's reconciliation sweep, not
///     this log line, is the ADR-level backstop for lost fiscal work;
/// (c) the one body this rule does subtract from is the one where "the log is the last copy" is a
///     liability rather than an asset: recovering a poisoned <c>send-email</c> is a RE-ISSUE
///     (ResendConfirmationEmail / RequestPasswordChange), which needs the user id and the purpose —
///     both in the clear inside the key — not a replay of a live token out of a vendor's log store.
/// Step 1 fails when Postgres does, which is not an independent per-message coin flip: every poisoned
/// message in that window takes this branch at once, so this is the burst case, not the singleton.</para>
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
