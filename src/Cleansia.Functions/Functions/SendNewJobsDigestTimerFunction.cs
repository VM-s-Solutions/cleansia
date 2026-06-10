using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendNewJobsDigestTimerHandler (Core).
/// <summary>Every 30 minutes (top and half of each hour). Sends the new-jobs digest to eligible
/// employees. Cron is read from the <c>SendNewJobsDigestCron</c> app-setting; production default is
/// <c>0 0,30 * * * *</c>. The digest watermark remains the duplicate-suppression mechanism.</summary>
public class SendNewJobsDigestTimerFunction(SendNewJobsDigestTimerHandler handler)
{
    [Function("SendNewJobsDigest")]
    public Task Run(
        [TimerTrigger("%SendNewJobsDigestCron%")] TimerInfo timer,
        CancellationToken ct)
        => handler.HandleAsync(ct);
}
