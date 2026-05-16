using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.DataRetention;

public interface IRefreshTokenCleanupService
{
    /// <summary>
    /// Hard-deletes refresh tokens that are both:
    ///  - no longer usable (revoked OR expired), AND
    ///  - at least <paramref name="retentionDays"/> old.
    /// Keeps recent revoked tokens for forensic / theft-investigation purposes.
    /// </summary>
    Task<int> CleanupAsync(int retentionDays = 90, CancellationToken cancellationToken = default);
}

public class RefreshTokenCleanupService(
    IRefreshTokenRepository repository,
    ILogger<RefreshTokenCleanupService> logger)
    : IRefreshTokenCleanupService
{
    public async Task<int> CleanupAsync(int retentionDays = 90, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        var deletedCount = await repository.DeleteStaleAsync(cutoff, cancellationToken);

        logger.LogInformation(
            "RefreshTokenCleanup deleted {Count} tokens older than {Cutoff:O}",
            deletedCount, cutoff);

        return deletedCount;
    }
}
