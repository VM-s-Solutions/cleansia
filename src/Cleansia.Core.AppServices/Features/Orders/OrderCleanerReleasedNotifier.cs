using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells the customer that a cleaner can no longer come and the booking is offered to cleaners again —
/// shared by the two release writers, <c>DropOrder</c> and <c>RejectEmployee</c>, which call it after every
/// release, the last cleaner's or one of several: every release leaves the job short, because an order has
/// no spare seat. The feed row, the push and the e-mail ride the caller's unit of work (no commit here), so
/// a release its commit refuses tells nobody.
///
/// <para>A guest has no account to push to and is e-mailed only; the e-mail goes to the booking's own
/// address either way. Both are keyed on the assignment that ended, so every release is its own
/// message.</para>
/// </summary>
public static class OrderCleanerReleasedNotifier
{
    /// <param name="releasedAssignmentId">
    /// The assignment that ended, captured before the unassign hard-deleted it — see
    /// <see cref="AssignmentNotificationSubject"/> for why the order id alone is not enough.
    /// </param>
    /// <param name="releasedAtUtc">The instant the writer ended the cleaner's reservation with, if it did.</param>
    public static async Task NotifyCustomerOfReleaseAsync(
        Order order,
        string releasedAssignmentId,
        DateTime releasedAtUtc,
        INotificationProducer notificationProducer,
        IPendingDispatch pendingDispatch,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(order.UserId))
        {
            await notificationProducer.NotifyAsync(
                order.UserId,
                NotificationEventCatalog.OrderCleanerReleased,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                },
                order.TenantId,
                AssignmentNotificationSubject.For(order.Id, releasedAssignmentId),
                cancellationToken);
        }

        var emailKey = MessageKeys.OrderCleanerReleasedEmail(order.Id, releasedAssignmentId);
        pendingDispatch.Enqueue(QueueNames.SendEmail,
            new QueueEnvelope<SendOrderCleanerReleasedEmailMessage>(emailKey, order.TenantId,
                new SendOrderCleanerReleasedEmailMessage(
                    order.Id, releasedAssignmentId, EmailLocale.Resolve(order.LanguageCode), order.TenantId)),
            emailKey);

        // This notice announces a reservation that has ended by now, so the lapse sweep sends no closure
        // after it. One another cleaner still holds is left to the sweep. → /product/business-rules#crew-lost
        if (order.PreferredHoldUntilUtc <= releasedAtUtc)
        {
            order.MarkPreferredOfferLapseNotified(releasedAtUtc);
        }
    }
}
