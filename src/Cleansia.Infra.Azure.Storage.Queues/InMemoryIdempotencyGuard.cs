using System.Collections.Concurrent;
using Cleansia.Core.Queue.Abstractions;

namespace Cleansia.Infra.Azure.Storage.Queues;

/// <summary>
/// In-memory backing for <see cref="IIdempotencyGuard"/> (sibling to
/// <see cref="InMemoryPendingDispatch"/>). Registered as a <b>singleton</b> so a claim survives across
/// redeliveries within one worker process — that is what makes a redelivery short-circuit before
/// re-sending the email.
///
/// <para>The claim lives only in this process's memory, so it does not dedup across a worker restart
/// or across scaled-out worker instances; the accepted residual is a rare duplicate notification email
/// under those conditions — never a duplicate fiscal artifact. A durable backing (a unique
/// <c>ProcessedMessage</c> row) would close that gap with no change to this interface.</para>
/// </summary>
public sealed class InMemoryIdempotencyGuard : IIdempotencyGuard
{
    private readonly ConcurrentDictionary<string, byte> _claimed = new();

    public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
        Task.FromResult(!_claimed.TryAdd(messageKey, 0));
}
