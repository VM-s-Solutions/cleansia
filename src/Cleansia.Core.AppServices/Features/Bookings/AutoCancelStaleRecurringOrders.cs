using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Sweeps Pending recurring-template Orders whose <c>CleaningDateTime</c> is
/// past the missed-confirm cutoff (default 1h before the cleaning slot) and
/// auto-cancels them with reason <c>missed_confirm</c>. Also dispatches an
/// <c>order.cancelled</c> push so the customer learns their slot's gone.
///
/// Pairs with <see cref="SendRecurringOrderReminders"/> (24h-ahead reminder)
/// and <see cref="Cleansia.Core.AppServices.Features.Orders.ConfirmRecurringOrder"/> (the customer's confirm path) —
/// this is the safety net that frees the cleaner's slot when the customer
/// either ignored the reminder or never opened it. Cancellation here is fee-
/// free (no payment was ever taken) — we just transition state + notify.
///
/// <para><b>A dispatcher, not a worker.</b> It selects the occurrence ids and sends one
/// <see cref="AutoCancelRecurringOccurrence"/> per id, each in its OWN DI scope — its own DbContext, change
/// tracker, tenant provider and pending-dispatch buffer — so an occurrence that fails takes its changes away
/// with its scope, holds back none of the others, and is retried on the next run. → /decisions/adr-0072</para>
/// </summary>
public class AutoCancelStaleRecurringOrders
{
    public const int DefaultMissedConfirmGraceHours = 1;
    public const int MaxMissedConfirmGraceHours = 24;

    /// <param name="MissedConfirmGraceHours">
    /// Hours before <c>CleaningDateTime</c> after which an unconfirmed
    /// recurring order is considered stale. Default 1h: the customer had ~23h
    /// since the reminder push to confirm; if they didn't, free the slot
    /// before the cleaner shows up to nothing.
    /// </param>
    public record Command(int MissedConfirmGraceHours = DefaultMissedConfirmGraceHours) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.MissedConfirmGraceHours).InclusiveBetween(0, MaxMissedConfirmGraceHours);
        }
    }

    /// <summary>
    /// <paramref name="Considered"/> counts the occurrences still awaiting confirmation at the cutoff;
    /// <paramref name="Failed"/> counts the ones whose unit threw or returned a failure. Each failed one stays
    /// as it was and is retried on the next run, so a non-zero count every hour is the alerting signal for an
    /// occurrence that is permanently stuck.
    /// </summary>
    public record Response(int Cancelled, int Considered, int Failed = 0);

    public class Handler(
        IOrderRepository orderRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow.AddHours(command.MissedConfirmGraceHours);

            // Ids only, across every company: each unit loads its occurrence in its own scope and stamps it with the
            // order's own company. The terms are Order.AwaitsCustomerConfirmation's in SQL: a confirmed cash
            // occurrence stays Pending until the cleaner records the cash, and is the cleaner's job, not this
            // sweep's; an occurrence cancelled before it was paid stays Pending too, and is done.
            var orderIds = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.RecurringTemplateId != null
                    && o.CurrentStatus != OrderStatus.Cancelled
                    && o.CurrentStatus != OrderStatus.Completed
                    && o.PaymentStatus == PaymentStatus.Pending
                    && (o.PaymentType != PaymentType.Cash || o.CustomerConfirmedAt == null)
                    && o.CleaningDateTime <= cutoff
                    && o.UserId != null)
                .OrderBy(o => o.CleaningDateTime)
                .ThenBy(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var cancelled = 0;
            var failed = 0;

            foreach (var orderId in orderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                        new AutoCancelRecurringOccurrence.Command(orderId, cutoff), cancellationToken);

                    if (result.IsSuccess && result.Value != null)
                    {
                        cancelled += result.Value.Cancelled ? 1 : 0;
                    }
                    else
                    {
                        failed++;
                        logger.LogError(
                            "Recurring occurrence {OrderId} failed to auto-cancel: {Error}. It stays as it was and "
                            + "the next run retries it",
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
                        "Recurring occurrence {OrderId} threw while auto-cancelling. Its scope is discarded with "
                        + "every change it staged, and the next run retries it",
                        orderId);
                }
            }

            return BusinessResult.Success(new Response(cancelled, orderIds.Count, failed));
        }
    }
}
