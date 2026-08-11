using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells the customer a cleaner is now committed to their order — shared by every path that creates an
/// assignment row (the cleaner's take, the admin reassign) so the two cannot disagree. Feed row + push
/// ride the caller's unit of work via the producer seam (no commit here).
///
/// <para>Fired on the assignment itself, never on a status transition: a card order is already
/// <see cref="Cleansia.Core.Domain.Enums.OrderStatus.Confirmed"/> by the time a cleaner can take it
/// (the Stripe webhook wrote that when the payment cleared), so gating this on the transition silences
/// the one moment a cleaner actually appears. Guest orders have nobody to tell.</para>
/// </summary>
public static class OrderCleanerAssignedNotifier
{
    public static Task NotifyCustomerOfAssignmentAsync(
        Order order,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order.UserId))
        {
            return Task.CompletedTask;
        }

        return notificationProducer.NotifyAsync(
            order.UserId,
            NotificationEventCatalog.OrderCleanerAssigned,
            new Dictionary<string, string>
            {
                ["orderId"] = order.Id,
                ["orderNumber"] = order.DisplayOrderNumber,
            },
            order.TenantId,
            order.Id,
            cancellationToken);
    }
}
