using Cleansia.Core.AppServices.Features.DataRetention;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// The testable timer-handler body for the outbox retention prune. The [TimerTrigger] shell stays in the
/// Exe; this Core body drives one prune sweep per wakeup so Cleansia.Tests can reference it. The sweep
/// deletes already-terminal rows only (Dispatched outbox / old processed-inbox) and never affects dispatch
/// or idempotency behavior.
/// </summary>
public class PruneOutboxTimerHandler(
    IMediator mediator,
    ILogger<PruneOutboxTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new PruneOutbox.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            if (result.Value.PrunedOutboxCount > 0 || result.Value.PrunedProcessedCount > 0)
            {
                logger.LogInformation(
                    "PruneOutbox completed; removed {OutboxCount} dispatched outbox rows and {ProcessedCount} processed-inbox rows",
                    result.Value.PrunedOutboxCount, result.Value.PrunedProcessedCount);
            }
        }
        else
        {
            logger.LogError("PruneOutbox failed: {Error}", result.Error?.Message ?? "unknown");
        }
    }
}
