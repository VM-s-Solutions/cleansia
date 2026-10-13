using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendRecurringOrderRemindersHandler (Core).
/// <summary>Hourly at :30 UTC. The cron is read from <c>SendRecurringOrderRemindersCron</c>;
/// the default is <c>0 30 * * * *</c>. Each occurrence's sent-at stamp prevents duplicates.</summary>
public class SendRecurringOrderRemindersFunction(SendRecurringOrderRemindersHandler handler)
{
    [Function("SendRecurringOrderReminders")]
    public Task Run([TimerTrigger("%SendRecurringOrderRemindersCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
