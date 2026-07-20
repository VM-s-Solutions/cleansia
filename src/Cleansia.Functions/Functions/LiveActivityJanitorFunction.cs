using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in LiveActivityJanitorTimerHandler (Core).
// Daily at 04:00 UTC (after the 02:00–03:30 recurring/cleanup band so they don't contend for the DB).
public class LiveActivityJanitorFunction(LiveActivityJanitorTimerHandler handler)
{
    [Function("LiveActivityJanitor")]
    public Task Run([TimerTrigger("0 0 4 * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
