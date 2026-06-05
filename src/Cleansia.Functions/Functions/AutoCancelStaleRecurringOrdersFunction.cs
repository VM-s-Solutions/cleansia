using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in AutoCancelStaleRecurringOrdersHandler (Core).
public class AutoCancelStaleRecurringOrdersFunction(AutoCancelStaleRecurringOrdersHandler handler)
{
    [Function("AutoCancelStaleRecurringOrders")]
    public Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
