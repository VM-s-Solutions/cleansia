using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in RetryFailedUserDeletionsTimerHandler (Core).
public class RetryFailedUserDeletionsFunction(RetryFailedUserDeletionsTimerHandler handler)
{
    // Daily at 05:00 UTC, clear of the 03:00–04:00 retention janitors it shares the small hours with.
    [Function("RetryFailedUserDeletions")]
    public Task Run([TimerTrigger("0 0 5 * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
