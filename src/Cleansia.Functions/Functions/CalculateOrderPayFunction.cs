using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in CalculateOrderPayHandler (Core).
public class CalculateOrderPayFunction(CalculateOrderPayHandler handler)
{
    [Function("CalculateOrderPay")]
    public Task Run(
        [QueueTrigger("calculate-order-pay", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
