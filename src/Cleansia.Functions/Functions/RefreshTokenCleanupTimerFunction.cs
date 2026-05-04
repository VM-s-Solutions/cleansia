using Cleansia.Core.AppServices.Features.DataRetention;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

/// <summary>
/// Nightly cleanup of stale refresh tokens. Runs at 03:30 UTC (30 min after the
/// main data-retention job so they don't contend for DB connections). Tokens
/// that are revoked or expired AND older than 90 days are hard-deleted — we
/// keep recent revoked ones to preserve rotation-theft detection history.
/// </summary>
public class RefreshTokenCleanupTimerFunction(
    IRefreshTokenCleanupService cleanupService,
    ILogger<RefreshTokenCleanupTimerFunction> logger)
{
    [Function("RefreshTokenCleanup")]
    public async Task Run([TimerTrigger("0 30 3 * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("RefreshTokenCleanup timer triggered at {Time}", DateTime.UtcNow);
        var deleted = await cleanupService.CleanupAsync(cancellationToken: ct);
        logger.LogInformation("RefreshTokenCleanup completed; deleted {Count} tokens", deleted);
    }
}
