namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Periodic sweep that fans out a "new jobs available near you" push to
/// cleaners who have at least one newly-eligible order since their last
/// digest. Fired by <c>SendNewJobsDigestTimerFunction</c> on a 30-min
/// cadence; idempotent on its own (re-runs no-op when nothing new).
/// </summary>
public interface INewJobsDigestService
{
    Task SendDigestsAsync(CancellationToken cancellationToken = default);
}
