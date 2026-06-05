using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in CleanupStalePendingOrdersHandler (Core).
public class CleanupStalePendingOrdersFunction(CleanupStalePendingOrdersHandler handler)
{
    [Function("CleanupStalePendingOrders")]
    public Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
