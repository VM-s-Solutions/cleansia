using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D3 (F3) — thin -poison trigger shell; body lives in CalculateOrderPayPoisonHandler (Core).
public class CalculateOrderPayPoisonFunction(CalculateOrderPayPoisonHandler handler)
{
    [Function("CalculateOrderPayPoison")]
    public Task Run(
        [QueueTrigger("calculate-order-pay-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
