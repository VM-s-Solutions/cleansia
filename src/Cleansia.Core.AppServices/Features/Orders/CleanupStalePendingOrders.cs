using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Sweep card-payment orders stuck in <see cref="PaymentStatus.Pending"/> past
/// the 1-hour mark. These are typically users who opened PaymentSheet but
/// closed it without confirming. Leaving them in Pending pollutes the matching
/// pool (cleaners can't pick them up), confuses admin dashboards, and races
/// with Stripe's own ~24h PaymentIntent expiry.
///
/// Marks them Cancelled with a system reason. Webhook handlers for any
/// eventually-canceled PaymentIntent will then no-op via the existing
/// idempotency check.
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
        ITenantProvider tenantProvider,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-command.OlderThanHours);

            // System job — no JWT context. Use IgnoreQueryFilters to see rows
            // across all tenants, then group + set override per tenant so
            // child writes (OrderStatusTrack) inherit the right TenantId.
            var stale = await orderRepository.GetQueryableIgnoringTenant()
                .Where(o => o.PaymentStatus == PaymentStatus.Pending
                    && o.PaymentType == PaymentType.Card
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
                    cancelledCount++;
                }
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
