using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Sweeps unconfirmed recurring occurrences due in roughly the next 24 hours and pushes a reminder to
/// confirm and pay. <b>Idempotency is the sent-at stamp on the order</b>, which the sweep filters on, so
/// running repeatedly inside the window fires once. → /flows/booking-and-pricing#recurring-bookings
/// </summary>
public class SendRecurringOrderReminders
{
    public record Command(int LeadHoursLow = 6, int LeadHoursHigh = 26) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LeadHoursLow).InclusiveBetween(1, 72);
            RuleFor(x => x.LeadHoursHigh).GreaterThan(x => x.LeadHoursLow);
        }
    }

    public record Response(int RemindersSent, int Considered);

    public class Handler(
        IOrderRepository orderRepository,
        INotificationProducer notificationProducer,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddHours(command.LeadHoursLow);
            var windowEnd = now.AddHours(command.LeadHoursHigh);

            // Cross-tenant query — the sweep runs system-level (no JWT). The
            // queue dispatch carries each order's TenantId so the dispatcher
            // routes it correctly downstream.
            var due = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.RecurringTemplateId != null
                    && o.RecurringReminderSentAt == null
                    && o.PaymentStatus == PaymentStatus.Pending
                    && o.CleaningDateTime >= windowStart
                    && o.CleaningDateTime <= windowEnd
                    && o.UserId != null)
                .ToListAsync(cancellationToken);

            var sent = 0;
            foreach (var order in due)
            {
                try
                {
                    await notificationProducer.NotifyAsync(
                        order.UserId!,
                        NotificationEventCatalog.RecurringScheduled,
                        new Dictionary<string, string>
                        {
                            ["orderId"] = order.Id,
                            ["orderNumber"] = order.DisplayOrderNumber,
                        },
                        order.TenantId,
                        order.Id,
                        cancellationToken);

                    order.MarkRecurringReminderSent(now);

                    // The sent-stamp and the reminder's outbox row commit together, so the row is durable
                    // iff the stamp is. A failed commit leaves RecurringReminderSentAt null and writes no
                    // row, so the next sweep retries this order.
                    await unitOfWork.CommitAsync(cancellationToken);
                    sent++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to enqueue recurring reminder for order {OrderId}",
                        order.Id);
                }
            }

            return BusinessResult.Success(new Response(sent, due.Count));
        }
    }
}
