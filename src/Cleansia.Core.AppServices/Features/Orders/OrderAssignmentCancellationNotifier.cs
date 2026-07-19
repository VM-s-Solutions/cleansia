using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Notifies every cleaner assigned to a cancelled order — shared by the customer and admin cancel
/// handlers so both paths behave identically. A cleaner who accepted a job learns nothing today when
/// it's cancelled; this closes that gap. Feed rows + push ride the caller's unit of work via the
/// producer seam (no commit here). Assignments whose Employee has no linked User (legacy rows) are
/// skipped rather than throwing.
/// </summary>
public static class OrderAssignmentCancellationNotifier
{
    public static async Task NotifyAssignedEmployeesOfCancellationAsync(
        Order order,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken)
    {
        foreach (var assignment in order.AssignedEmployees)
        {
            var employeeUserId = assignment.Employee?.UserId;
            if (string.IsNullOrEmpty(employeeUserId))
            {
                continue;
            }

            await notificationProducer.NotifyAsync(
                employeeUserId,
                NotificationEventCatalog.OrderAssignmentCancelled,
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
}
