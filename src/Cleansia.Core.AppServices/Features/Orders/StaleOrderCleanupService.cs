using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

public class StaleOrderCleanupService(
    IOrderRepository orderRepository,
    ILogger<StaleOrderCleanupService> logger)
    : IStaleOrderCleanupService
{
    /// <summary>
    /// Stripe checkout sessions expire after 24 hours by default.
    /// We use 30 minutes as threshold since abandoned checkouts are unlikely to be completed after that.
    /// </summary>
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);
    private const int BatchSize = 50;

    public async Task CancelStaleOrdersAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Stale order cleanup job started");

        var cutoff = DateTimeOffset.UtcNow.Subtract(StaleThreshold);
        var totalCancelled = 0;

        while (true)
        {
            var staleOrders = await orderRepository.GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .Where(o => o.PaymentType == PaymentType.Card
                         && o.PaymentStatus == PaymentStatus.Pending
                         && o.OrderStatusHistory.Any(h => h.Status == OrderStatus.Pending)
                         && !o.OrderStatusHistory.Any(h => h.Status == OrderStatus.Confirmed
                                                        || h.Status == OrderStatus.Cancelled)
                         && o.CreatedOn < cutoff)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (staleOrders.Count == 0) break;

            foreach (var order in staleOrders)
            {
                order.UpdatePaymentStatus(PaymentStatus.Failed);
                order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
            }

            await orderRepository.CommitAsync(cancellationToken);
            totalCancelled += staleOrders.Count;
        }

        logger.LogInformation("Stale order cleanup completed. Cancelled {Total} abandoned orders", totalCancelled);
    }
}
