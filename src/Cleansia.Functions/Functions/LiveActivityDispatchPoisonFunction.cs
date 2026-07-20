using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D3 (F3) — thin -poison trigger shell; body lives in LiveActivityDispatchPoisonHandler (Core).
public class LiveActivityDispatchPoisonFunction(LiveActivityDispatchPoisonHandler handler)
{
    [Function("LiveActivityDispatchPoison")]
    public Task Run(
        [QueueTrigger("live-activity-dispatch-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
