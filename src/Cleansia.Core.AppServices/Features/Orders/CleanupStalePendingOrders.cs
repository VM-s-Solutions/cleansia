using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
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

    public record Response(int CancelledCount);

    public class Handler(
        IOrderRepository orderRepository,
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-command.OlderThanHours);

            var stale = await orderRepository.GetQueryable()
                .Where(o => o.PaymentStatus == PaymentStatus.Pending
                    && o.PaymentType == PaymentType.Card
                    && o.CreatedOn < cutoff)
                .Include(o => o.OrderStatusHistory)
                .ToListAsync(cancellationToken);

            int cancelledCount = 0;
            foreach (var order in stale)
            {
                // Skip if some other process already moved this order out of Pending.
                if (order.PaymentStatus != PaymentStatus.Pending)
                {
                    continue;
                }

                order.UpdatePaymentStatus(PaymentStatus.Failed);
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
                cancelledCount++;
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
