using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendPreCleaningRemindersHandler (Core).
/// <summary>Every five minutes. Sends the customer the T-1h "your cleaning starts soon" reminder the
/// booking-confirmation screen promises. Cron is read from the <c>SendPreCleaningRemindersCron</c>
/// app-setting; production default is <c>0 *&#47;5 * * * *</c>. The cadence is bound to the sweep's
/// fifteen-minute window rather than chosen freely — a gap wider than the window would let orders pass
/// through un-swept, which is what <c>TimerScheduleConfigTests</c> asserts. The
/// <c>PreCleaningReminderSentAt</c> per-entity stamp remains the duplicate-suppression mechanism.</summary>
public class SendPreCleaningRemindersFunction(SendPreCleaningRemindersHandler handler)
{
    [Function("SendPreCleaningReminders")]
    public Task Run([TimerTrigger("%SendPreCleaningRemindersCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
