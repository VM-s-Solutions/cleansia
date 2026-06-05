using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0120 / ADR-0002 D3 (F3) — thin -poison trigger shell; body lives in NotificationsDispatchPoisonHandler (Core).
public class NotificationsDispatchPoisonFunction(NotificationsDispatchPoisonHandler handler)
{
    [Function("NotificationsDispatchPoison")]
    public Task Run(
        [QueueTrigger("notifications-dispatch-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
