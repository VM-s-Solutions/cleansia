using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// Thin trigger shell; the prune body lives in PruneOutboxTimerHandler (Core). Daily off-peak: retention is
// table-growth hygiene with no time-sensitivity, and the prune only deletes already-terminal rows.
public class PruneOutboxFunction(PruneOutboxTimerHandler handler)
{
    [Function("PruneOutbox")]
    public Task Run([TimerTrigger("0 0 4 * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
