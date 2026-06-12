using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendRecurringOrderRemindersHandler (Core).
/// <summary>Daily at 02:30 UTC — strictly after MaterializeRecurringBookings (02:00 UTC) so the
/// same-day orders it creates are already present when this sweep reminds. Cron is read from the
/// <c>SendRecurringOrderRemindersCron</c> app-setting; production default is <c>0 30 2 * * *</c>.
/// The <c>RecurringReminderSentAt</c> per-entity stamp remains the duplicate-suppression mechanism.</summary>
public class SendRecurringOrderRemindersFunction(SendRecurringOrderRemindersHandler handler)
{
    [Function("SendRecurringOrderReminders")]
    public Task Run([TimerTrigger("%SendRecurringOrderRemindersCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
