using System.Text.Json;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Infra.Azure.Storage.Queues;

/// <summary>
/// ADR-0002 D1 — the Wave-0 in-memory backing for <see cref="IPendingDispatch"/>. Registered SCOPED
/// (per request): it buffers a command's intended post-commit queue sends and hands them to
/// <c>PostCommitDispatchBehavior</c> on <see cref="Drain"/>.
///
/// <para>D1.1 invariant — <see cref="Enqueue{T}"/> is idempotent within a request on
/// <c>(QueueName, MessageKey)</c>: a second call with the same key buffers nothing extra.</para>
///
/// <para>Wave-0 is honestly <b>at-most-once</b> for the dispatch step — a crash between commit and
/// drain loses the in-memory buffer (Wave-1 / F2-FULL swaps this backing for a durable outbox row).
/// The buffer is serialized eagerly to the same camelCase JSON the <see cref="AzureStorageQueueClient"/>
/// uses, so the dispatcher can send the body verbatim.</para>
/// </summary>
public sealed class InMemoryPendingDispatch : IPendingDispatch
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Insertion order preserved; the key set enforces the (QueueName, MessageKey) idempotency.
    private readonly List<PendingMessage> _buffer = [];
    private readonly HashSet<(string Queue, string Key)> _seen = [];

    public void Enqueue<T>(string queueName, T message, string messageKey)
    {
        if (!_seen.Add((queueName, messageKey)))
        {
            // D1.1: same (QueueName, MessageKey) within this request → already buffered, no-op.
            return;
        }

        var body = JsonSerializer.Serialize(message, JsonOptions);
        _buffer.Add(new PendingMessage(queueName, body, messageKey));
    }

    public IReadOnlyList<PendingMessage> Drain()
    {
        var drained = _buffer.ToArray();
        _buffer.Clear();
        _seen.Clear();
        return drained;
    }
}
