using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in SendPushNotificationHandler (Core).
public class SendPushNotificationFunction(SendPushNotificationHandler handler)
{
    [Function("SendPushNotification")]
    public Task Run(
        [QueueTrigger("notifications-dispatch", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
