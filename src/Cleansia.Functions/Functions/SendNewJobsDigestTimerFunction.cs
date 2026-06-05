using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in SendNewJobsDigestTimerHandler (Core).
public class SendNewJobsDigestTimerFunction(SendNewJobsDigestTimerHandler handler)
{
    [Function("SendNewJobsDigest")]
    public Task Run(
        [TimerTrigger("0 0/2 * * * *")] TimerInfo timer,
        CancellationToken ct)
        => handler.HandleAsync(ct);
}
