using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendTomorrowJobDigestHandler (Core).
/// <summary>Hourly, on the hour. Tells each cleaner how many jobs they have tomorrow, at 18:00 in
/// their own local time. Cron is read from the <c>SendTomorrowJobDigestCron</c> app-setting;
/// production default is <c>0 0 * * * *</c>.
///
/// <para>Hourly is load-bearing, not a cadence preference: a timer fires on a UTC cron and cannot be
/// timezone-aware, so the sweep runs every hour and picks the cleaners whose LOCAL clock has just
/// struck the send hour. A daily tick could only ever be right for one timezone.</para></summary>
public class SendTomorrowJobDigestFunction(SendTomorrowJobDigestHandler handler)
{
    [Function("SendTomorrowJobDigest")]
    public Task Run([TimerTrigger("%SendTomorrowJobDigestCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
