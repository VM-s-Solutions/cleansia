using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Refunds;

/// <summary>
/// Every hour, re-drive the card refunds of cancelled orders that Stripe refused or could not be reached
/// for, each on the key it was created with, and tell the company's administrators about any refund still
/// owed and not through a day after it was asked for, saying whether this job retries it or they must
/// (owner ruling 2026-09-28). A refund with nothing left to give back is closed instead. A refund Stripe
/// made on the key and then failed is closed too, and raised for a retry at once, however young.
/// → /flows/cancellation-refund-dispute#refund
/// </summary>
public class RedrivePendingRefunds
{
    /// <param name="MinimumAgeMinutes">
    /// A refund younger than this may still be in flight with the request that created it; the shared key
    /// would make a race harmless at Stripe, but the first attempt deserves to finish.
    /// </param>
    /// <param name="AlertAfterHours">How long a refund may stay pending before the administrators are told.</param>
    public record Command(int MinimumAgeMinutes = 30, int AlertAfterHours = 24) : ICommand<Response>;

    public record Response(int Considered, int Redriven, int Alerted, int Failed = 0);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.MinimumAgeMinutes).InclusiveBetween(1, 1440);
            RuleFor(x => x.AlertAfterHours).InclusiveBetween(1, 720);
        }
    }

    public class Handler(
        IRefundRepository refundRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var youngest = now.AddMinutes(-command.MinimumAgeMinutes);
            var alertBefore = now.AddHours(-command.AlertAfterHours);
            var ids = await refundRepository.GetQueryableIgnoringTenant()
                .AsNoTracking()
                .Where(r => r.Status == RefundStatus.Pending && r.Source == RefundSource.AppRefund && r.CreatedOn <= youngest)
                .OrderBy(r => r.CreatedOn).ThenBy(r => r.Id)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
            var redriven = 0;
            var alerted = 0;
            var failed = 0;
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    BusinessResult<RedrivePendingRefund.Response> result;
                    await using (var scope = serviceScopeFactory.CreateAsyncScope())
                        result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                            new RedrivePendingRefund.Command(id, youngest, alertBefore), cancellationToken);
                    if (result.IsSuccess && result.Value is { } value)
                    {
                        redriven += value.Redriven ? 1 : 0;
                        alerted += value.Alerted ? 1 : 0;
                    }
                    else
                    {
                        failed++;
                        logger.LogError("Refund {RefundId} failed to re-drive: {Error}; the next run tries again",
                            id, result.Error?.Message ?? "unknown");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "Could not re-drive refund {RefundId}; its scope is discarded and the next run tries again", id);
                }
            }
            if (ids.Count > 0)
                logger.LogInformation("RedrivePendingRefunds considered {Considered} pending refunds: {Redriven} re-driven, {Alerted} raised to administrators, {Failed} failed",
                    ids.Count, redriven, alerted, failed);
            return BusinessResult.Success(new Response(ids.Count, redriven, alerted, failed));
        }
    }
}
