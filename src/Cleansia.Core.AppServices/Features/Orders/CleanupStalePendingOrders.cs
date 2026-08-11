using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Sweep one-off card orders stuck in <see cref="PaymentStatus.Pending"/> past
/// the 1-hour mark. These are typically users who opened PaymentSheet but
/// closed it without confirming. Leaving them in Pending pollutes the matching
/// pool (cleaners can't pick them up), confuses admin dashboards, and races
/// with Stripe's own ~24h PaymentIntent expiry.
///
/// Marks them Cancelled and tells the customer. Webhook handlers for any
/// eventually-canceled PaymentIntent will then no-op via the existing
/// idempotency check.
///
/// Recurring occurrences are deliberately out of scope — see the query below;
/// <see cref="Cleansia.Core.AppServices.Features.Bookings.AutoCancelStaleRecurringOrders"/>
/// owns their retraction.
/// </summary>
public class CleanupStalePendingOrders
{
    public record Command(int OlderThanHours = 1) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OlderThanHours).InclusiveBetween(1, 168);
        }
    }

    public record Response(int CancelledCount);

    public class Handler(
        IOrderRepository orderRepository,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider,
        IUnitOfWork unitOfWork,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-command.OlderThanHours);

            // System job — no JWT context. Use IgnoreQueryFilters to see rows
            // across all tenants, then group + set override per tenant so
            // child writes (OrderStatusTrack, the feed row) inherit the right TenantId.
            //
            // RecurringTemplateId == null is load-bearing. A recurring occurrence is materialized up
            // to 7 days before its slot and stays Pending until the customer confirms it via
            // ConfirmRecurringOrder, so "Pending for over an hour" is its NORMAL state, not an
            // abandoned checkout. Retracting unconfirmed occurrences belongs to
            // AutoCancelStaleRecurringOrders, which fires at T-1h once the reminder has gone
            // unanswered; this term is the exact complement of that sweep's RecurringTemplateId !=
            // null, so every Pending order has exactly one retractor.
            var stale = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.PaymentStatus == PaymentStatus.Pending
                    && o.PaymentType == PaymentType.Card
                    && o.RecurringTemplateId == null
                    && o.CreatedOn < cutoff)
                .Include(o => o.OrderStatusHistory)
                .ToListAsync(cancellationToken);

            int cancelledCount = 0;
            foreach (var tenantGroup in stale.GroupBy(o => o.TenantId ?? string.Empty))
            {
                // Reset before each iteration so a non-empty override from the
                // previous group doesn't leak into a single-tenant (empty key)
                // group that follows it.
                tenantProvider.ClearTenantOverride();
                if (!string.IsNullOrEmpty(tenantGroup.Key))
                {
                    tenantProvider.SetTenantOverride(tenantGroup.Key);
                }

                foreach (var order in tenantGroup)
                {
                    if (order.PaymentStatus != PaymentStatus.Pending)
                    {
                        continue;
                    }

                    order.UpdatePaymentStatus(PaymentStatus.Failed);
                    order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

                    if (!string.IsNullOrEmpty(order.UserId))
                    {
                        await notificationProducer.NotifyAsync(
                            order.UserId,
                            NotificationEventCatalog.OrderCancelled,
                            new Dictionary<string, string>
                            {
                                ["orderId"] = order.Id,
                                ["orderNumber"] = order.DisplayOrderNumber,
                            },
                            order.TenantId,
                            order.Id,
                            cancellationToken);
                    }

                    cancelledCount++;
                }

                // The commit is what makes the override above mean anything: rows added by this group
                // are stamped from the ambient tenant AT COMMIT TIME, so deferring to the pipeline's
                // single commit would stamp every group with whichever tenant was processed last.
                await unitOfWork.CommitAsync(cancellationToken);
            }

            if (cancelledCount > 0)
            {
                logger.LogInformation(
                    "CleanupStalePendingOrders cancelled {Count} stale card orders older than {Hours}h",
                    cancelledCount, command.OlderThanHours);
            }

            return BusinessResult.Success(new Response(cancelledCount));
        }
    }
}
