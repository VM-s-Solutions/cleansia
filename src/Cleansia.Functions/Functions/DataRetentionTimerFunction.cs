using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in DataRetentionTimerHandler (Core).
public class DataRetentionTimerFunction(DataRetentionTimerHandler handler)
{
    [Function("DataRetentionCleanup")]
    public Task Run([TimerTrigger("0 0 3 * * 0")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
