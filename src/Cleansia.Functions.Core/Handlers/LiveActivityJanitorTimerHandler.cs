using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// The 24h stale-activity janitor (ADR-0029 D3, cleanup path 3). ActivityKit auto-ends stale
/// activities after ~8h; this reclaims the orphaned per-order <c>LiveActivityToken</c> rows a lost
/// terminal update would otherwise leave behind. Order-scoped rows ONLY — push-to-start rows are
/// per-install and never swept (<see cref="LiveActivityJanitorPolicy"/>). Cross-tenant (the timer has
/// no JWT) — the repository sweep bypasses the tenant filter deliberately.
/// </summary>
public class LiveActivityJanitorTimerHandler(
    ILiveActivityTokenRepository liveActivityTokenRepository,
    ILogger<LiveActivityJanitorTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var cutoff = LiveActivityJanitorPolicy.StaleCutoff(DateTimeOffset.UtcNow);
        var stale = await liveActivityTokenRepository.GetStaleOrderScopedTokensAsync(cutoff, ct);

        if (stale.Count == 0)
        {
            logger.LogInformation("Live-activity janitor: no stale order-scoped tokens older than {Cutoff}", cutoff);
            return;
        }

        liveActivityTokenRepository.RemoveRange(stale);
        await liveActivityTokenRepository.CommitAsync(ct);

        logger.LogInformation("Live-activity janitor: reclaimed {Count} stale order-scoped tokens older than {Cutoff}", stale.Count, cutoff);
    }
}
