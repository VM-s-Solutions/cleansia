using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendMembershipLifecycleNotificationsHandler (Core).
public class SendMembershipLifecycleNotificationsFunction(SendMembershipLifecycleNotificationsHandler handler)
{
    [Function("SendMembershipLifecycleNotifications")]
    public Task Run([TimerTrigger("0 */2 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
