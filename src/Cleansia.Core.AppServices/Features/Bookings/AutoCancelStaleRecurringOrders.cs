using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
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
/// and <see cref="ConfirmRecurringOrder"/> (the customer's confirm path) —
/// this is the safety net that frees the cleaner's slot when the customer
/// either ignored the reminder or never opened it. Cancellation here is fee-
/// free (no payment was ever taken) — we just transition state + notify.
/// </summary>
public class AutoCancelStaleRecurringOrders
{
    /// <param name="MissedConfirmGraceHours">
    /// Hours before <c>CleaningDateTime</c> after which an unconfirmed
    /// recurring order is considered stale. Default 1h: the customer had ~23h
    /// since the reminder push to confirm; if they didn't, free the slot
    /// before the cleaner shows up to nothing.
    /// </param>
    public record Command(int MissedConfirmGraceHours = 1) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.MissedConfirmGraceHours).InclusiveBetween(0, 24);
        }
    }

    public record Response(int Cancelled, int Considered);

    public class Handler(
        IOrderRepository orderRepository,
        IPendingDispatch pendingDispatch,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var nowUtc = DateTime.UtcNow;
            var cutoff = nowUtc.AddHours(command.MissedConfirmGraceHours);

            // Cross-tenant query — sweep runs system-level. Each cancellation
            // dispatches a push tagged with the order's TenantId so the
            // dispatcher routes it correctly.
            var stale = await orderRepository.GetQueryableIgnoringTenant()
                .Include(o => o.OrderStatusHistory)
                .Where(o => o.RecurringTemplateId != null
                    && o.PaymentStatus == PaymentStatus.Pending
                    && o.CleaningDateTime <= cutoff
                    && o.UserId != null)
                .ToListAsync(cancellationToken);

            var cancelled = 0;
            foreach (var order in stale)
            {
                // Filter out orders already terminated (Cancelled or post-Confirmed)
                // — the SQL filter on PaymentStatus catches most but the in-memory
                // status check is cheap insurance against race conditions where a
                // status flipped between query and processing.
                var currentStatus = order.GetCurrentOrderStatus();
                if (currentStatus is OrderStatus.Cancelled or OrderStatus.Completed)
                {
                    continue;
                }

                try
                {
                    order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

                    var messageKey = MessageKeys.Push(
                        order.UserId!, NotificationEventCatalog.OrderCancelled, order.Id);
                    pendingDispatch.Enqueue(
                        QueueNames.NotificationsDispatch,
                        new QueueEnvelope<SendPushNotificationMessage>(
                            messageKey,
                            order.TenantId,
                            new SendPushNotificationMessage(
                                UserId: order.UserId!,
                                EventKey: NotificationEventCatalog.OrderCancelled,
                                Args: new Dictionary<string, string>
                                {
                                    ["orderId"] = order.Id,
                                    ["orderNumber"] = order.DisplayOrderNumber,
                                },
                                TenantId: order.TenantId)),
                        messageKey);

                    // The cancellation and its outbox row commit together so the row is durable iff this
                    // order's state changed; the drainer puts it on the wire after the commit.
                    await unitOfWork.CommitAsync(cancellationToken);
                    cancelled++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to auto-cancel stale recurring order {OrderId}",
                        order.Id);
                }
            }

            return BusinessResult.Success(new Response(cancelled, stale.Count));
        }
    }
}
