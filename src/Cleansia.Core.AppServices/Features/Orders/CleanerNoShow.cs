using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// A cleaner who did not arrive is established by an administrator, never by a timer (owner ruling
/// 2026-09-28): the reminder sweep and the customer's report raise the alert, and the administrator's
/// confirmation cancels, refunds and apologises. → /product/business-rules#when-the-cleaner-cancels-or-no-shows
/// </summary>
public static class CleanerNoShow
{
    /// <summary>
    /// Why no-show cannot be reported or confirmed on this order right now, or <see langword="null"/>
    /// when it can: the booked start has passed and nobody has started the job.
    /// </summary>
    public static string? RefusalFor(Order order, DateTime nowUtc) => order.CurrentStatus switch
    {
        OrderStatus.Cancelled => BusinessErrorMessage.OrderAlreadyCancelled,
        OrderStatus.Completed => BusinessErrorMessage.OrderAlreadyCompleted,
        OrderStatus.InProgress => BusinessErrorMessage.OrderCleanerAlreadyStarted,
        _ when nowUtc < order.CleaningDateTime => BusinessErrorMessage.OrderStartTimeNotReached,
        _ => null,
    };

    /// <summary>
    /// Tells the order's company once per order, whichever of the sweep and the report comes first:
    /// the e-mail keys its outbox row on the order, so a second raise would fail the commit. True when
    /// this call raised it. Only for an order with a cleaner assigned; every caller checks that first.
    /// </summary>
    public static async Task<bool> AlertAsync(
        Order order,
        IAdminNotifier adminNotifier,
        IUserNotificationRepository userNotificationRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order.TenantId)
            || await userNotificationRepository.AnyForEventAsync(
                order.TenantId, AdminNotificationEventCatalog.OrderCleanerNotStarted, "orderId", order.Id,
                cancellationToken))
        {
            return false;
        }

        await adminNotifier.NotifyAsync(
            new AdminEvent(
                AdminNotificationEventCatalog.OrderCleanerNotStarted,
                order.TenantId,
                Subject: order.Id,
                Args: new Dictionary<string, string>
                {
                    ["orderNumber"] = order.DisplayOrderNumber,
                    ["cleaningDateTime"] = DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc)
                        .ToString("O", CultureInfo.InvariantCulture),
                    ["orderId"] = order.Id,
                }),
            cancellationToken);
        return true;
    }
}
