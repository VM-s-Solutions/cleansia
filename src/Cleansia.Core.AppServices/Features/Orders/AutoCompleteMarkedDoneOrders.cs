using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// A crew job that everyone on it marked done, and that nobody completed, closes itself two hours after the
/// last mark, through the completion a cleaner's Complete runs (owner ruling 2026-10-26: "If everyone marked
/// that they're done, but didn't click on a shared button then yes. If not then they're still cleaning"). A
/// job with anyone on it still unmarked is never closed here, and no administrator is told.
///
/// <para><b>A dispatcher, not a worker.</b> It selects the job ids across every company and sends one
/// <see cref="AutoCompleteMarkedDoneOrder"/> per id, each in its OWN DI scope, so a job that fails takes its
/// changes away with its scope, holds back none of the others, and is retried on the next run.
/// → /decisions/adr-0072</para>
/// </summary>
public class AutoCompleteMarkedDoneOrders
{
    public const int DefaultGraceMinutes = 120;
    public const int MinGraceMinutes = 15;
    public const int MaxGraceMinutes = 720;

    /// <param name="GraceMinutes">How long after its crew's last mark a job nobody completed closes itself.</param>
    public record Command(int GraceMinutes = DefaultGraceMinutes) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.GraceMinutes).InclusiveBetween(MinGraceMinutes, MaxGraceMinutes);
        }
    }

    /// <summary>
    /// <paramref name="Considered"/> counts the jobs selected; <paramref name="Failed"/> the ones whose unit threw
    /// or returned a failure, each left as it was and retried on the next run.
    /// </summary>
    public record Response(int Completed, int Considered, int Failed = 0);

    public class Handler(
        IOrderRepository orderRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-command.GraceMinutes);

            // Ids only, across every company: each unit reads its job again under its row lock, in its own
            // scope, and completes it under the job's own company.
            var orderIds = await AutoCompleteMarkedDoneOrder.Qualifying(orderRepository.GetQueryableIgnoringTenant(), cutoff)
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var completed = 0;
            var failed = 0;

            foreach (var orderId in orderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                        new AutoCompleteMarkedDoneOrder.Command(orderId, cutoff), cancellationToken);

                    if (result.IsSuccess && result.Value != null)
                    {
                        completed += result.Value.Completed ? 1 : 0;
                    }
                    else
                    {
                        failed++;
                        logger.LogError(
                            "Marked-done order {OrderId} failed to complete: {Error}. It stays as it was and the next "
                            + "run retries it",
                            orderId,
                            result.Error?.Message ?? "unknown");
                    }
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
                        "Marked-done order {OrderId} threw while completing. Its scope is discarded with every "
                        + "change it staged, and the next run retries it",
                        orderId);
                }
            }

            return BusinessResult.Success(new Response(completed, orderIds.Count, failed));
        }
    }
}
