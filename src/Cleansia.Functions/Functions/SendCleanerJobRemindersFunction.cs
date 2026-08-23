using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendCleanerJobRemindersHandler (Core).
/// <summary>Every five minutes. Reminds a cleaner that a job they took starts in about two hours, and
/// nudges them close to the start if they still have not set off. Cron is read from the
/// <c>SendCleanerJobRemindersCron</c> app-setting; production default is <c>0 *&#47;5 * * * *</c>.
/// The cadence is bound to the sweep's windows rather than chosen freely — a gap wider than the
/// narrower window would let assignments pass through un-swept. The per-assignment
/// <c>ReminderSoonSentAt</c> / <c>ReminderNotStartedSentAt</c> stamps remain the duplicate-suppression
/// mechanism.</summary>
public class SendCleanerJobRemindersFunction(SendCleanerJobRemindersHandler handler)
{
    [Function("SendCleanerJobReminders")]
    public Task Run([TimerTrigger("%SendCleanerJobRemindersCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
