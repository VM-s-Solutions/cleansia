namespace Cleansia.Core.Queue.Abstractions;

/// <summary>
/// ADR-0002 D1 — the dispatch contract. A command handler <b>records intent</b> to send a queue
/// message; it never performs the external queue side effect directly. The recorded intent is
/// realized <b>after</b> the owning <c>UnitOfWork</c> commit succeeds, by
/// <c>PostCommitDispatchBehavior</c> (the outermost pipeline behavior).
///
/// Registered <b>SCOPED</b> (per request). Wave-0 backing is a pure in-memory buffer
/// (<c>InMemoryPendingDispatch</c>); Wave-1 (F2-FULL) swaps the backing to write an outbox row into
/// the same scoped <c>DbContext</c> the pipeline commits — with <b>zero</b> command-handler call-site
/// churn, which is the whole point of this seam.
/// </summary>
public interface IPendingDispatch
{
    /// <summary>
    /// Records intent to send <paramref name="message"/> to <paramref name="queueName"/> after the
    /// unit of work commits. <paramref name="messageKey"/> is the deterministic idempotency key (D2.1).
    ///
    /// <para>D1.1 invariant — <b>idempotent within a request</b> on <c>(QueueName, MessageKey)</c>:
    /// calling this twice with the same key in one request buffers <b>exactly one</b> message.</para>
    /// </summary>
    void Enqueue<T>(string queueName, T message, string messageKey);

    /// <summary>
    /// Hands the buffered messages to the dispatcher and <b>clears</b> the buffer (D1.2). On any
    /// non-success the pipeline simply never calls this and the scoped buffer is discarded with the
    /// scope.
    /// </summary>
    IReadOnlyList<PendingMessage> Drain();
}

/// <summary>
/// A buffered, not-yet-dispatched queue send. <see cref="Body"/> is the already-serialized wire body
/// (a serialized <see cref="QueueEnvelope{T}"/>); the dispatcher sends it verbatim.
/// </summary>
public sealed record PendingMessage(string QueueName, string Body, string MessageKey);
