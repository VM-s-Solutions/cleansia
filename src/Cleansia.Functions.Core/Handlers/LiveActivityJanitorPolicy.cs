namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// The 24h stale-activity reclaim window the janitor sweeps by (ADR-0029 D3, cleanup path 3).
/// ActivityKit force-ends an activity after ~8h; the janitor reclaims the orphaned per-order token rows
/// a lost terminal update would otherwise leave behind. The exclusion of push-to-start rows
/// (<c>OrderId == null</c>, per-install, NEVER swept) lives in the repository query
/// <c>ILiveActivityTokenRepository.GetStaleOrderScopedTokensAsync</c> — only order-scoped rows past the
/// cutoff age out.
/// </summary>
public static class LiveActivityJanitorPolicy
{
    /// <summary>
    /// The activity max-lifetime the janitor reclaims past — comfortably beyond the OS ~8h force-end so
    /// a live activity's rows are never pruned mid-service.
    /// </summary>
    public static readonly TimeSpan MaxActivityLifetime = TimeSpan.FromHours(24);

    public static DateTimeOffset StaleCutoff(DateTimeOffset now) => now - MaxActivityLifetime;
}
