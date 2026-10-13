using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Reminds unconfirmed recurring occurrences 2–26 hours ahead, each in its own scope. The notice and
/// sent-at stamp commit together; a refused occurrence leaves with its scope and the next tick retries it.
/// → /flows/booking-and-pricing#recurring-bookings
/// </summary>
public class SendRecurringOrderReminders
{
    public record Command(int LeadHoursLow = 2, int LeadHoursHigh = 26) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LeadHoursLow).InclusiveBetween(1, 72);
            RuleFor(x => x.LeadHoursHigh).GreaterThan(x => x.LeadHoursLow);
        }
    }

    public record Response(int RemindersSent, int Considered, int Failed = 0);

    public class Handler(
        IOrderRepository orderRepository,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddHours(command.LeadHoursLow);
            var windowEnd = now.AddHours(command.LeadHoursHigh);
            var orderIds = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.RecurringTemplateId != null
                    && o.CurrentStatus != OrderStatus.Cancelled
                    && o.CurrentStatus != OrderStatus.Completed
                    && o.RecurringReminderSentAt == null
                    && o.PaymentStatus == PaymentStatus.Pending
                    && (o.PaymentType != PaymentType.Cash || o.CustomerConfirmedAt == null)
                    && o.CleaningDateTime >= windowStart
                    && o.CleaningDateTime <= windowEnd
                    && o.UserId != null)
                .OrderBy(o => o.CleaningDateTime).ThenBy(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);

            var sent = 0;
            var failed = 0;
            foreach (var orderId in orderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await using var scope = serviceScopeFactory.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                        new SendRecurringOrderReminder.Command(orderId, now, windowStart, windowEnd), cancellationToken);
                    if (result.IsSuccess && result.Value != null)
                    {
                        sent += result.Value.Sent ? 1 : 0;
                    }
                    else
                    {
                        failed++;
                        logger.LogError("Recurring reminder for order {OrderId} failed: {Error}; the next tick retries it",
                            orderId, result.Error?.Message ?? "unknown");
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "Recurring reminder for order {OrderId} threw; its scope is discarded and the next tick retries it", orderId);
                }
            }
            return BusinessResult.Success(new Response(sent, orderIds.Count, failed));
        }
    }
}
