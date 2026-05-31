using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

/// <summary>
/// Periodic "new jobs available near you" digest for cleaners.
///
/// Cadence: every 30 minutes (cron <c>0 0,30 * * * *</c>). The sweep
/// itself is the rate-limit — each cleaner receives at most one digest
/// per interval, and only when they have at least one newly-eligible
/// order since their last watermark.
///
/// All targeting (work country, contract status, not-busy) + opt-out +
/// per-cleaner watermark advance live in <see cref="INewJobsDigestService"/>.
/// </summary>
public class SendNewJobsDigestTimerFunction(
    INewJobsDigestService digestService,
    ILogger<SendNewJobsDigestTimerFunction> logger)
{
    [Function("SendNewJobsDigest")]
    public async Task Run(
        [TimerTrigger("0 0/2 * * * *")] TimerInfo timer,
        CancellationToken ct)
    {
        logger.LogInformation("SendNewJobsDigest timer triggered at {Time}", DateTime.UtcNow);
        await digestService.SendDigestsAsync(ct);
        logger.LogInformation("SendNewJobsDigest completed");
    }
}
