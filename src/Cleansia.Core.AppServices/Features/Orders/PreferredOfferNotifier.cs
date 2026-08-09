using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Q-BROWSE-01 owner ruling (b) — the ONE producer of the cleaner-facing "this booking is held for
/// you" signal, and the one place the question "is there anything to announce yet" is asked. Sibling of
/// <see cref="PreferredOfferClosedNotifier"/>; feed row + push ride the caller's unit of work via the
/// producer seam (no commit here).
///
/// <para>The push deep-links to the order detail, whose browse gate is
/// <see cref="OrderAvailability"/>. So the announcement rides the transition into OFFERABLE, not the
/// creation: a cash one-off is offerable the moment it exists and is announced by the factory, while a
/// card order sits <c>New</c> + <c>Pending</c> until the Stripe webhook lands and a recurring
/// occurrence until the customer confirms. Announcing earlier hands a cleaner a notification whose
/// screen refuses them, and — for the card order CleanupStalePendingOrders cancels an hour later — one
/// that reads as "you were given a job that vanished".</para>
/// </summary>
public static class PreferredOfferNotifier
{
    /// <summary>
    /// For a caller that already holds the resolver's answer (the factory, and the customer's second
    /// choice). A null recipient is the resolver's "do not tell this cleaner" and is not a failure.
    /// </summary>
    public static Task NotifyIfOfferableAsync(
        Order order,
        PreferredCleanerRecipient? recipient,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken)
    {
        if (recipient is null || !IsOfferable(order))
        {
            return Task.CompletedTask;
        }

        return notificationProducer.NotifyAsync(
            recipient.UserId,
            NotificationEventCatalog.PreferredOffer,
            new Dictionary<string, string>
            {
                ["orderId"] = order.Id,
                ["orderNumber"] = order.DisplayOrderNumber,
            },
            recipient.TenantId,
            order.Id,
            cancellationToken);
    }

    /// <summary>
    /// For the sites that MAKE an order offerable and therefore owe the announcement the creation
    /// withheld: the Stripe webhook's completed session, and the customer's confirmation of a recurring
    /// cash occurrence.
    ///
    /// <para>The recipient is re-derived from the resolver rather than read off
    /// <c>Order.PreferredEmployeeId</c>, because that column records what the customer ASKED FOR and is
    /// written even when the resolver declined — a muted, unreachable, unapproved or already-busy
    /// cleaner. Reading the column alone would push exactly the cleaners ADR-0036 D4.1 decided not to
    /// push. Re-running it against a persisted order is the shape <c>ChoosePreferredCleaner</c> already
    /// uses, and it costs nothing on the overwhelming majority of orders, which carry no preference.</para>
    /// </summary>
    public static async Task NotifyBecameOfferableAsync(
        Order order,
        IPreferredCleanerHoldResolver preferredCleanerHoldResolver,
        INotificationProducer notificationProducer,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(order.PreferredEmployeeId) || !IsOfferable(order))
        {
            return;
        }

        var resolved = await preferredCleanerHoldResolver.ResolveAsync(
            order.UserId,
            order.PreferredEmployeeId,
            order.CustomerAddress?.CountryId,
            order.CleaningDateTime,
            order.EstimatedTime,
            nowUtc,
            cancellationToken);

        await NotifyIfOfferableAsync(order, resolved.Recipient, notificationProducer, cancellationToken);
    }

    private static bool IsOfferable(Order order) => OrderAvailability.IsOfferable(
        order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId);
}
