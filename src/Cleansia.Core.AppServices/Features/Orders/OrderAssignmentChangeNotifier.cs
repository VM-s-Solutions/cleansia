using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells a cleaner that somebody else changed their commitments — the admin reassign, which is the only
/// assignment write the cleaner is not the actor of. Both directions are covered because both cost the
/// cleaner a day: one gains a job they never chose, the other loses one they had planned around. Feed
/// rows + push ride the caller's unit of work via the producer seam (no commit here), so a reassignment
/// the pipeline refuses sends nothing. Cleaners with no linked User (legacy rows) are skipped rather
/// than throwing, matching <see cref="OrderAssignmentCancellationNotifier"/>.
/// </summary>
public static class OrderAssignmentChangeNotifier
{
    public static Task NotifyCleanerOfAssignmentAsync(
        Order order,
        Employee cleaner,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken) =>
        NotifyAsync(order, cleaner, NotificationEventCatalog.OrderAssigned, notificationProducer, cancellationToken);

    public static Task NotifyCleanerOfRevocationAsync(
        Order order,
        Employee cleaner,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken) =>
        NotifyAsync(order, cleaner, NotificationEventCatalog.OrderAssignmentRevoked, notificationProducer, cancellationToken);

    private static Task NotifyAsync(
        Order order,
        Employee cleaner,
        string eventKey,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cleaner.UserId))
        {
            return Task.CompletedTask;
        }

        return notificationProducer.NotifyAsync(
            cleaner.UserId,
            eventKey,
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
