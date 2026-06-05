using Cleansia.Core.AppServices.Features.DataRetention;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Nightly cleanup of stale refresh tokens. Runs at 03:30 UTC (30 min after the
/// main data-retention job so they don't contend for DB connections). Tokens
/// that are revoked or expired AND older than 90 days are hard-deleted — we
/// keep recent revoked ones to preserve rotation-theft detection history.
/// </summary>
public class RefreshTokenCleanupTimerHandler(
    IRefreshTokenCleanupService cleanupService,
    ILogger<RefreshTokenCleanupTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("RefreshTokenCleanup timer triggered at {Time}", DateTime.UtcNow);
        var deleted = await cleanupService.CleanupAsync(cancellationToken: ct);
        logger.LogInformation("RefreshTokenCleanup completed; deleted {Count} tokens", deleted);
    }
}
