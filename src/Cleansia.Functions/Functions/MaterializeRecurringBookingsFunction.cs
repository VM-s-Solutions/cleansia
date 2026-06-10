using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in MaterializeRecurringBookingsHandler (Core).
/// <summary>Daily at 02:00 UTC. Materializes due recurring bookings into orders before the recurring
/// reminder sweep runs (which is scheduled strictly after, at 02:30 UTC). Cron is read from the
/// <c>MaterializeRecurringBookingsCron</c> app-setting; production default is <c>0 0 2 * * *</c>.</summary>
public class MaterializeRecurringBookingsFunction(MaterializeRecurringBookingsHandler handler)
{
    [Function("MaterializeRecurringBookings")]
    public Task Run([TimerTrigger("%MaterializeRecurringBookingsCron%")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
