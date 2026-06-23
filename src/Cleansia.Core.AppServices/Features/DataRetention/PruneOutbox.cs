using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.DataRetention;

/// <summary>
/// Table-growth hygiene for the durable messaging tables: deletes terminal <see cref="OutboxMessageStatus.Dispatched"/>
/// outbox rows older than the retention window and processed-inbox idempotency rows older than their window.
/// This is read-terminal-then-delete only — it never touches a Pending/Failed outbox row (those are still
/// re-drivable) nor an in-flight idempotency claim, so dispatch and duplicate-suppression are unchanged. The
/// audit table is deliberately out of scope (append-only, keep-indefinitely). Deletes run in bounded,
/// per-batch-committed loops so a single run can never issue one unbounded DELETE and a crash keeps the
/// batches already removed.
/// </summary>
public class PruneOutbox
{
    public record Command : ICommand<Response>;

    public class Validator : AbstractValidator<Command>;

    public record Response(int PrunedOutboxCount, int PrunedProcessedCount);

    public class Handler(
        IOutboxMessageRepository outboxRepository,
        IProcessedMessageRepository processedRepository,
        IUnitOfWork unitOfWork,
        IOutboxRetentionConfig config,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (!config.Enabled)
            {
                return BusinessResult.Success(new Response(0, 0));
            }

            var now = DateTimeOffset.UtcNow;
            var prunedOutbox = await PruneDispatchedOutboxAsync(now, cancellationToken);
            var prunedProcessed = await PruneProcessedInboxAsync(now, cancellationToken);

            if (prunedOutbox > 0 || prunedProcessed > 0)
            {
                logger.LogInformation(
                    "Outbox retention prune removed {OutboxCount} dispatched outbox rows and {ProcessedCount} processed-inbox rows",
                    prunedOutbox, prunedProcessed);
            }

            return BusinessResult.Success(new Response(prunedOutbox, prunedProcessed));
        }

        private async Task<int> PruneDispatchedOutboxAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var cutoff = now.AddDays(-config.DispatchedRetentionDays);
            var total = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // System job — no JWT, so the tenant filter is bypassed to see every tenant's terminal rows.
                // Only Dispatched rows are ever eligible: a Pending/Failed row is still re-drivable and must
                // never be pruned. DispatchedOn is the terminal-transition time, the correct retention anchor.
                var batch = await outboxRepository.GetQueryableIgnoringTenant()
                    .Where(m => m.Status == OutboxMessageStatus.Dispatched
                        && m.DispatchedOn != null
                        && m.DispatchedOn < cutoff)
                    .OrderBy(m => m.DispatchedOn)
                    .Take(config.BatchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                outboxRepository.RemoveRange(batch);
                await unitOfWork.CommitAsync(cancellationToken);
                total += batch.Count;

                if (batch.Count < config.BatchSize)
                {
                    break;
                }
            }

            return total;
        }

        private async Task<int> PruneProcessedInboxAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var cutoff = now.UtcDateTime.AddDays(-config.ProcessedRetentionDays);
            var total = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = await processedRepository.GetQueryable()
                    .Where(m => m.ProcessedAt < cutoff)
                    .OrderBy(m => m.ProcessedAt)
                    .Take(config.BatchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                processedRepository.RemoveRange(batch);
                await unitOfWork.CommitAsync(cancellationToken);
                total += batch.Count;

                if (batch.Count < config.BatchSize)
                {
                    break;
                }
            }

            return total;
        }
    }
}
