using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendMembershipLifecycleNotificationsHandler (Core).
/// <summary>Daily at 03:00 UTC. Sweeps memberships for renewal/cancellation lifecycle reminders. Cron
/// is read from the <c>SendMembershipLifecycleNotificationsCron</c> app-setting; production default is
/// <c>0 0 3 * * *</c>. The <c>RenewalReminderSentAt</c>/<c>CancellationReminderSentAt</c> per-entity
/// stamps remain the duplicate-suppression mechanism.</summary>
public class SendMembershipLifecycleNotificationsFunction(SendMembershipLifecycleNotificationsHandler handler)
{
    [Function("SendMembershipLifecycleNotifications")]
    public Task Run([TimerTrigger("%SendMembershipLifecycleNotificationsCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
