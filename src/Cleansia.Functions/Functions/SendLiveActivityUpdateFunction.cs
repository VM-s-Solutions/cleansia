using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in SendLiveActivityUpdateHandler (Core).
public class SendLiveActivityUpdateFunction(SendLiveActivityUpdateHandler handler)
{
    [Function("SendLiveActivityUpdate")]
    public Task Run(
        [QueueTrigger("live-activity-dispatch", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
