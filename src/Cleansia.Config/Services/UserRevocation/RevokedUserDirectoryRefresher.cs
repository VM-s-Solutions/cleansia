using Cleansia.Config.Services.DeviceRevocation;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cleansia.Config.Services.UserRevocation;

/// <summary>
/// The background pump that keeps <see cref="RevokedUserDirectory"/> warm (ADR-0027 D2/D5). Every
/// <c>DeviceRevocation:RefreshSeconds</c> (the SHARED cadence, ADR-0027 D7) it reads the password
/// resets within the horizon (access-token TTL + 5 min slack: a reset older than the TTL cannot have a
/// live token predating it) and swaps the snapshot. On a poll fault it keeps the last snapshot
/// (fail-open) and warns once the snapshot is older than 3x the interval.
///
/// The loop is deliberately un-killable: the ENTIRE tick sits inside the loop's try/catch so no
/// exception can escape <see cref="ExecuteAsync"/>. .NET's default
/// <c>BackgroundServiceExceptionBehavior.StopHost</c> would otherwise turn a poll bug into a host
/// crash (a catastrophic fail-closed), and a dead loop can never emit the staleness warning that is
/// the only ops signal - the warner must be the survivor.
/// </summary>
public sealed class RevokedUserDirectoryRefresher(
    RevokedUserDirectory directory,
    IServiceScopeFactory scopeFactory,
    IJwtSettings jwtSettings,
    IOptions<DeviceRevocationOptions> options,
    TimeProvider timeProvider,
    ILogger<RevokedUserDirectoryRefresher> logger)
    : BackgroundService
{
    private const double HorizonSlackMinutes = 5;

    private TimeSpan Interval => TimeSpan.FromSeconds(Math.Max(1, options.Value.RefreshSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // One synchronous initial fill attempt; empty-on-failure, never crash/block the host.
        await RefreshOnceAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RefreshOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// One poll tick. Wraps the whole body in try/catch so nothing escapes into <see cref="ExecuteAsync"/>.
    /// Public so the host tests can force a deterministic refresh instead of racing the timer.
    /// </summary>
    public async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = timeProvider.GetUtcNow();
            var horizon = TimeSpan.FromMinutes(jwtSettings.AccessTokenExpMinutes + HorizonSlackMinutes);
            var cutoff = now - horizon;

            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
            var resets = await repository.GetPasswordResetsSinceAsync(cutoff, cancellationToken);

            var entries = resets
                .Select(r => new RevokedUserEntry(r.UserId, r.ResetAt))
                .ToArray();

            directory.Replace(entries, now);
        }
        catch (Exception ex)
        {
            var lastPolledAt = directory.LastPolledAt;
            var age = lastPolledAt is null ? (TimeSpan?)null : timeProvider.GetUtcNow() - lastPolledAt.Value;

            if (age is null || age.Value > 3 * Interval)
            {
                logger.LogWarning(ex,
                    "RevokedUserDirectory poll failed; serving a stale snapshot (age {AgeSeconds:F0}s, reset-cutoff enforcement degrades toward the 30-min TTL backstop)",
                    age?.TotalSeconds ?? double.PositiveInfinity);
            }
            else
            {
                logger.LogInformation(ex, "RevokedUserDirectory poll failed; last snapshot still fresh, retrying next tick");
            }
        }
    }
}
