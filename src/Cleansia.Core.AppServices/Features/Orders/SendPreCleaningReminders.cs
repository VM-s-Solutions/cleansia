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

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Sweeps one-off orders whose cleaning starts in about an hour and sends the customer the
/// <c>order.starting_soon</c> push the booking-confirmation screen promises them.
///
/// <para><b>Who qualifies</b> — both lifecycle axes, never the fulfilment one alone.
/// <c>CurrentStatus == Confirmed</c> is the only status the cleaning is still ahead of: <c>New</c> means
/// nobody has taken it (cash) or the card payment has not settled, <c>OnTheWay</c>/<c>InProgress</c>
/// mean the customer has already been told the work is starting, and the two terminal states have
/// nothing to remind about. But <c>Confirmed</c> is overloaded — the Stripe webhook writes it with no
/// cleaner on the job — so it is conjoined with an assignment row. The money term is
/// <c>OrderAvailability</c>'s, specialised to <c>RecurringTemplateId == null</c>: a one-off card order
/// still <c>Pending</c> is what <c>CleanupStalePendingOrders</c> cancels on its next tick.</para>
///
/// <para><b>Window</b> — <c>CleaningDateTime</c> in <c>[now + 55min, now + 70min]</c>, swept every five
/// minutes, so the reminder lands between 55 and 70 minutes ahead and the copy's "about an hour" holds
/// across the whole range. Closed at the LOWER end on purpose: a missed tick costs silence rather than a
/// reminder that arrives after the hour it promised, and a reminder cannot be un-sent.</para>
///
/// <para>Deliberately unlike <see cref="Cleansia.Core.AppServices.Features.Bookings.SendRecurringOrderReminders"/>,
/// which is a daily 24h-ahead prompt asking the customer to CONFIRM an occurrence. Different audience
/// state, different lead time, different ask — only the sweep idiom is shared.</para>
/// </summary>
public class SendPreCleaningReminders
{
    public record Command(int LeadMinutesLow = 55, int LeadMinutesHigh = 70) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.LeadMinutesLow).InclusiveBetween(1, 240);
            RuleFor(x => x.LeadMinutesHigh).GreaterThan(x => x.LeadMinutesLow);
        }
    }

    public record Response(int RemindersSent, int Considered);

    public class Handler(
        IOrderRepository orderRepository,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(command.LeadMinutesLow);
            var windowEnd = now.AddMinutes(command.LeadMinutesHigh);

            var due = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.CurrentStatus == OrderStatus.Confirmed
                    && o.RecurringTemplateId == null
                    && o.PreCleaningReminderSentAt == null
                    && o.CleaningDateTime >= windowStart
                    && o.CleaningDateTime <= windowEnd
                    && o.UserId != null
                    && o.AssignedEmployees.Any()
                    && (o.PaymentStatus == PaymentStatus.Paid || o.PaymentType == PaymentType.Cash))
                .ToListAsync(cancellationToken);

            var sent = 0;
            foreach (var tenantGroup in due.GroupBy(o => o.TenantId ?? string.Empty))
            {
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var order in tenantGroup)
                {
                    try
                    {
                        await notificationProducer.NotifyAsync(
                            order.UserId!,
                            NotificationEventCatalog.OrderStartingSoon,
                            new Dictionary<string, string>
                            {
                                ["orderId"] = order.Id,
                                ["orderNumber"] = order.DisplayOrderNumber,
                            },
                            order.TenantId,
                            order.Id,
                            cancellationToken);

                        order.MarkPreCleaningReminderSent(now);
                        sent++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "Failed to enqueue pre-cleaning reminder for order {OrderId}",
                            order.Id);
                    }
                }

                // The stamp and the reminder's outbox row commit together, so the row is durable iff the
                // stamp is and a failed commit leaves the whole group unreminded for the next tick. The
                // commit is also what makes the override above mean anything: rows added by this group are
                // stamped from the ambient tenant AT COMMIT TIME, so deferring to one commit at the end
                // would stamp every group with whichever tenant was processed last.
                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (sent > 0)
            {
                logger.LogInformation(
                    "SendPreCleaningReminders sent {Sent} of {Considered} reminders for cleanings {Low}-{High} minutes out",
                    sent, due.Count, command.LeadMinutesLow, command.LeadMinutesHigh);
            }

            return BusinessResult.Success(new Response(sent, due.Count));
        }
    }
}
