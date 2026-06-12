using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in ExpireStaleReferralsHandler (Core).
/// <summary>Daily at 03:30 UTC. Flips Accepted referrals past the 90-day qualifying window to Expired.
/// Cron is read from the <c>ExpireStaleReferralsCron</c> app-setting; production default is
/// <c>0 30 3 * * *</c>. The Accepted-only <c>GetExpirableAsync</c> cutoff filter is the
/// duplicate-suppression mechanism — a re-run never re-expires a terminal row.</summary>
public class ExpireStaleReferralsFunction(ExpireStaleReferralsHandler handler)
{
    [Function("ExpireStaleReferrals")]
    public Task Run([TimerTrigger("%ExpireStaleReferralsCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
