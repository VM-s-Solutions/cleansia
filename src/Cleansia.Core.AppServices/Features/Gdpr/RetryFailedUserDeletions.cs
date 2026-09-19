using System.Globalization;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// Daily sweep over deletion requests that did not complete — <c>Failed</c>, or left <c>Processing</c>
/// by a host that died mid-walk — re-running each erasure once per row per day. A dispatcher, not a
/// worker: every request is retried through <see cref="AdminRetryUserDeletion"/> in its OWN DI scope, so
/// a walk that fails leaves its staged changes in a context nobody else uses, its failure is stamped on
/// its own row out of band, and the next row starts clean.
/// </summary>
public static class RetryFailedUserDeletions
{
    public record Command : ICommand<Response>;

    public class Validator : AbstractValidator<Command>;

    /// <summary>
    /// <paramref name="Failed"/> is the alarm: a request that stays in it day after day is stuck. Each
    /// failed attempt tells the row's company through the admin feed and e-mail, once per row per day,
    /// beside the Error log line.
    /// </summary>
    public record Response(int Candidates, int Completed, int Failed);

    public class Handler(
        IGdprRequestRepository gdprRequestRepository,
        IDataRetentionConfig retentionConfig,
        IServiceScopeFactory serviceScopeFactory,
        TimeProvider timeProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (!retentionConfig.Enabled)
            {
                logger.LogWarning("Failed-deletion retry disabled by configuration (DataRetention:Enabled). Skipping");
                return BusinessResult.Success(new Response(0, 0, 0));
            }

            var now = timeProvider.GetUtcNow();
            var startOfToday = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            var day = startOfToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var staleProcessingBefore = now - AdminRetryUserDeletion.StaleProcessingAfter;

            // Ids and tenants only: the row is loaded again inside the per-request scope that erases it.
            // The last attempt is the row's UpdatedOn — a failed attempt bumps it through the out-of-band
            // note, so "once per day" needs no column of its own.
            var candidates = await gdprRequestRepository.GetQueryableIgnoringTenant()
                .Where(r => r.RequestType == GdprRequest.DeletionRequestType
                    && ((r.Status == GdprRequestStatus.Failed && (r.UpdatedOn ?? r.CreatedOn) < startOfToday)
                        || (r.Status == GdprRequestStatus.Processing && (r.UpdatedOn ?? r.CreatedOn) < staleProcessingBefore)))
                .OrderBy(r => r.CreatedOn)
                .Select(r => new { r.Id, r.TenantId })
                .ToListAsync(cancellationToken);

            var completed = 0;
            var failed = 0;

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    if (!string.IsNullOrEmpty(candidate.TenantId))
                    {
                        scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(candidate.TenantId);
                    }

                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new AdminRetryUserDeletion.Command(candidate.Id), cancellationToken);

                    if (result.IsSuccess)
                    {
                        completed++;
                        continue;
                    }

                    failed++;
                    logger.LogError(
                        "Deletion request {RequestId} failed again on retry: {Error}. It stays Failed with the note appended and is retried tomorrow.",
                        candidate.Id,
                        result.Error?.Message ?? "unknown");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(
                        ex,
                        "Deletion request {RequestId} threw on retry. Its scope is discarded, the failure is on the row, and it is retried tomorrow.",
                        candidate.Id);
                }

                await TellAdministratorsAsync(candidate.Id, candidate.TenantId, day, cancellationToken);
            }

            return BusinessResult.Success(new Response(candidates.Count, completed, failed));
        }

        /// <summary>
        /// The failed retry's own scope is discarded with its staged walk, so the rows that tell the
        /// company go in a scope of their own, committed there. The day is in the subject because a
        /// request that fails again tomorrow is meant to be heard again; the candidate predicate is
        /// what keeps one day to one attempt, and so to one event. Best-effort like the failure record:
        /// a company that cannot be told never stops the sweep.
        /// </summary>
        private async Task TellAdministratorsAsync(string requestId, string? tenantId, string day, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                logger.LogWarning("Failed deletion request {RequestId} has no company, so no administrator can be told", requestId);
                return;
            }

            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantOverride(tenantId);
                await scope.ServiceProvider.GetRequiredService<IAdminNotifier>().NotifyAsync(
                    new AdminEvent(
                        AdminNotificationEventCatalog.ErasureFailed,
                        tenantId,
                        Subject: $"{requestId}:{day}",
                        Args: new Dictionary<string, string>
                        {
                            ["day"] = day,
                            ["requestId"] = requestId,
                        }),
                    cancellationToken);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The administrators could not be told that deletion request {RequestId} failed again", requestId);
            }
        }
    }
}
