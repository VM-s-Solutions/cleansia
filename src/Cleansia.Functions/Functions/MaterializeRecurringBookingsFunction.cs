using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in MaterializeRecurringBookingsHandler (Core).
public class MaterializeRecurringBookingsFunction(MaterializeRecurringBookingsHandler handler)
{
    [Function("MaterializeRecurringBookings")]
    public Task Run([TimerTrigger("0 */2 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
