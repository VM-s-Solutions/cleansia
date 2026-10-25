using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells the customer that the last cleaner left their order and another one is being looked for —
/// shared by the two release writers, <c>DropOrder</c> and <c>RejectEmployee</c>, which call it exactly
/// when <see cref="Order.ReturnToBoardIfUnstaffed"/> walks the order back, so a crew that remains or an
/// order that was not Confirmed tells the customer nothing. The feed row, the push and the e-mail ride the
/// caller's unit of work (no commit here), so a release its commit refuses tells nobody.
///
/// <para>A guest has no account to push to and is e-mailed only; the e-mail goes to the booking's own
/// address either way. Both are keyed on the assignment that ended, so every walk-back is its own
/// message.</para>
/// </summary>
public static class OrderCleanerReleasedNotifier
{
    /// <param name="releasedAssignmentId">
    /// The assignment that ended, captured before the unassign hard-deleted it — see
    /// <see cref="AssignmentNotificationSubject"/> for why the order id alone is not enough.
    /// </param>
    public static async Task NotifyCustomerOfReleaseAsync(
        Order order,
        string releasedAssignmentId,
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
    }
}
