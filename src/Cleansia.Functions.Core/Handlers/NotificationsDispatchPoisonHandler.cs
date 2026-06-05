using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for <c>notifications-dispatch</c>. Logs+alerts+stores the
/// poisoned push body (store at minimum) and acks. See <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class NotificationsDispatchPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<NotificationsDispatchPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.NotificationsDispatch;
}
