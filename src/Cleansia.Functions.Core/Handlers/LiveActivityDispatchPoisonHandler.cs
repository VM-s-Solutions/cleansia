using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for <c>live-activity-dispatch</c>. Persists the poisoned body
/// (durable record) + LogError + acks. See <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class LiveActivityDispatchPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<LiveActivityDispatchPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.LiveActivityDispatch;
}
