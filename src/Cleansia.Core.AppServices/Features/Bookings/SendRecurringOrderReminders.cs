using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Sweeps Pending recurring-template Orders due in roughly the next 24 hours
/// and dispatches a <c>recurring.scheduled</c> push to the customer reminding
/// them to confirm + pay before the cleaning slot. Pairs with
/// <see cref="MaterializeRecurringBookings"/> (which spawns the Order rows
/// 7 days ahead) and Wave 3.3's <c>ConfirmRecurringOrder</c> command (which
/// the customer reaches via the push deep-link).
///
/// Idempotency: each Order has a <see cref="Order.RecurringReminderSentAt"/>
/// stamp. The sweep filters by null on that field, so running multiple times
/// inside the 24h window only fires once per order.
///
/// Window: Orders with <c>CleaningDateTime</c> between <c>now + leadHoursLow</c>
/// and <c>now + leadHoursHigh</c>. Defaulted to [22, 26] so a sweep at 02:00 UTC
/// catches everything roughly 24h out, with slack so a cron miss-by-a-few-hours
/// doesn't drop reminders. Orders past the upper bound get caught by next-day
/// sweeps as long as they cross into the window before the cleaning starts.
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
        IQueueClient queueClient,
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
                    await queueClient.SendAsync(
                        QueueNames.NotificationsDispatch,
                        new SendPushNotificationMessage(
                            UserId: order.UserId!,
                            EventKey: NotificationEventCatalog.RecurringScheduled,
                            Args: new Dictionary<string, string>
                            {
                                ["orderId"] = order.Id,
                                ["orderNumber"] = order.DisplayOrderNumber,
                            },
                            TenantId: order.TenantId),
                        cancellationToken);

                    order.MarkRecurringReminderSent(now);
                    sent++;
                }
                catch (Exception ex)
                {
                    // Log + skip — a transient queue failure shouldn't kill the
                    // whole sweep. The order's RecurringReminderSentAt stays null
                    // so the next sweep will retry.
                    logger.LogWarning(ex,
                        "Failed to enqueue recurring reminder for order {OrderId}",
                        order.Id);
                }
            }

            return BusinessResult.Success(new Response(sent, due.Count));
        }
    }
}
