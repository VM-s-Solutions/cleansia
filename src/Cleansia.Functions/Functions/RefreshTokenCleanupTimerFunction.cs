using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in RefreshTokenCleanupTimerHandler (Core).
public class RefreshTokenCleanupTimerFunction(RefreshTokenCleanupTimerHandler handler)
{
    [Function("RefreshTokenCleanup")]
    public Task Run([TimerTrigger("0 30 3 * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
