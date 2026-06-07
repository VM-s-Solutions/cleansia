using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// Thin trigger shell; body lives in SendEmailHandler (Core).
public class SendEmailFunction(SendEmailHandler handler)
{
    [Function("SendEmail")]
    public Task Run(
        [QueueTrigger("send-email", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
