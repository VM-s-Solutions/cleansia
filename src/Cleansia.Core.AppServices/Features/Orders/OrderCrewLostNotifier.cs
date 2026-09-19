using System.Globalization;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The company's administrators are told whenever a release leaves nobody on an order — at ANY
/// non-terminal status, not only when the order is walked back to New. An order dropped OnTheWay or
/// InProgress is never walked back (a cleaner may be in the home) and no sweep selects it, so this is
/// the only thing that says a customer is waiting for nobody. Shared by the two release writers so the
/// event they raise cannot drift apart; the swaps (reassign, cover take) replace rather than release
/// and never call it.
/// </summary>
public static class OrderCrewLostNotifier
{
    public const string Dropped = "dropped";
    public const string Rejected = "rejected";

    /// <param name="releasedAssignmentId">
    /// The assignment that ended, captured before the unassign hard-deleted it — with the order id it
    /// makes the subject unique per release, so a re-take-and-re-drop is a second event and never a
    /// repeat of the first.
    /// </param>
    /// <param name="statusAtLoss">The order's status when the crew emptied, read BEFORE any walk-back.</param>
    public static Task NotifyAsync(
        Order order,
        string releasedAssignmentId,
        string cause,
        OrderStatus statusAtLoss,
        IAdminNotifier adminNotifier,
        CancellationToken cancellationToken)
    {
        if (order.TenantId is null)
        {
            return Task.CompletedTask;
        }

        return adminNotifier.NotifyAsync(
            new AdminEvent(
                AdminNotificationEventCatalog.OrderCrewLost,
                order.TenantId,
                Subject: AssignmentNotificationSubject.For(order.Id, releasedAssignmentId),
                Args: new Dictionary<string, string>
                {
                    ["orderNumber"] = order.DisplayOrderNumber,
                    ["cause"] = cause,
                    ["statusAtLoss"] = statusAtLoss.ToString(),
                    ["cleaningDateTime"] = DateTime.SpecifyKind(order.CleaningDateTime, DateTimeKind.Utc)
                        .ToString("O", CultureInfo.InvariantCulture),
                    ["orderId"] = order.Id,
                }),
            cancellationToken);
    }
}
