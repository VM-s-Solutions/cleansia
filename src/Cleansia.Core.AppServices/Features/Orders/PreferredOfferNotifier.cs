using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The ONE producer of the cleaner-facing "this booking is held for you" signal. Feed row and push ride
/// the caller's unit of work; no commit here.
///
/// <para><b>The announcement rides the transition into OFFERABLE, not creation.</b> Announcing earlier
/// hands a cleaner a notification whose screen refuses them — and for a card order the stale-checkout
/// sweep cancels an hour later, so it reads as "you were given a job that vanished".
/// → /flows/offerability-and-take</para>
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
    /// For the sites that MAKE an order offerable and therefore owe the announcement creation withheld.
    ///
    /// <para><b>The recipient is re-derived from the resolver, never read off
    /// <c>Order.PreferredEmployeeId</c></b> — that column records what the customer ASKED FOR and is
    /// written even when the resolver declined a muted, unreachable, unapproved or busy cleaner. Reading
    /// it alone would push exactly the cleaners the hold rule decided not to.
    /// → /domain/offerability#the-preferred-cleaner-hold</para>
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
