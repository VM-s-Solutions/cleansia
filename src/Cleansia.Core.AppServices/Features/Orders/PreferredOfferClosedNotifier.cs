using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// ADR-0045 D6.4 — the ONE producer of the customer-facing "your favourite didn't take it" signal, with
/// two callers: the 5-minute lapse sweep and the cleaner's explicit decline. Sharing the producer is
/// what makes the message, the args and the receipt stamp unable to diverge, and that equality is the
/// whole of D7.3's guarantee — a per-path string would tell the customer WHICH way the offer ended.
/// Feed row + push ride the caller's unit of work via the producer seam (no commit here).
///
/// <para>The receipt is stamped for every order, including a guest's: it is what makes the sweep skip
/// an order the decline already announced, so an order with nobody to tell must still be marked or the
/// sweep re-selects it every five minutes forever.</para>
/// </summary>
public static class PreferredOfferClosedNotifier
{
    public static Task NotifyPreferredOfferClosedAsync(
        Order order,
        INotificationProducer notificationProducer,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        order.MarkPreferredOfferLapseNotified(nowUtc);

        if (string.IsNullOrEmpty(order.UserId))
        {
            return Task.CompletedTask;
        }

        return notificationProducer.NotifyAsync(
            order.UserId,
            NotificationEventCatalog.PreferredOfferClosed,
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
