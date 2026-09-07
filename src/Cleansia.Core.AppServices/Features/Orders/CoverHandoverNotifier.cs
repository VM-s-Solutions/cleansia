using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Tells a cleaner their cover request was answered — somebody took the job, and it is off their
/// schedule.
///
/// <para><b>Reuses <c>order.assignment_revoked</c> rather than minting a key.</b> That key was
/// registered, translated in five locales across the partner Android app and both iOS catalogues, and
/// then never sent by anything — it was written for <c>AdminReassignOrder</c>'s replaced cleaner and
/// no production code ever produced one. Its shipped copy is "Job #N has been reassigned and is no
/// longer on your schedule", which is exactly true here and names no admin. A purpose-made key would
/// have cost the same sentence in ten places to say the same thing.</para>
///
/// <para><b>The dedup subject is the ASSIGNMENT, never the bare order id.</b> One order hands over
/// more than once across its life — A asks for cover, B takes it, B asks for cover, C takes it — and
/// with the order id alone the second handover would mint a key the first had already written, hit the
/// outbox unique index at commit, and roll the whole take back. That is a shipped defect this
/// primitive exists to prevent, not a hypothetical. → <see cref="AssignmentNotificationSubject"/></para>
///
/// <para>Reads the employee by id rather than off the assignment's navigation: the row is hard-deleted
/// by the swap before anyone is told, so the navigation is gone. A cleaner with no linked user is
/// skipped rather than throwing, exactly as <see cref="OrderAssignmentCancellationNotifier"/> does.</para>
/// </summary>
public static class CoverHandoverNotifier
{
    public static async Task NotifyCoverAnsweredAsync(
        Order order,
        string coveredEmployeeId,
        string coveredAssignmentId,
        IEmployeeRepository employeeRepository,
        INotificationProducer notificationProducer,
        CancellationToken cancellationToken)
    {
        var covered = await employeeRepository.GetByIdAsync(coveredEmployeeId, cancellationToken);
        if (string.IsNullOrEmpty(covered?.UserId))
        {
            return;
        }

        await notificationProducer.NotifyAsync(
            covered.UserId,
            NotificationEventCatalog.OrderAssignmentRevoked,
            new Dictionary<string, string>
            {
                ["orderId"] = order.Id,
                ["orderNumber"] = order.DisplayOrderNumber,
            },
            order.TenantId,
            AssignmentNotificationSubject.For(order.Id, coveredAssignmentId),
            cancellationToken);
    }
}
