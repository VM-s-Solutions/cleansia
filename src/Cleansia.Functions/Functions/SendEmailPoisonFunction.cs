using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// Thin -poison trigger shell; body lives in SendEmailPoisonHandler (Core).
public class SendEmailPoisonFunction(SendEmailPoisonHandler handler)
{
    [Function("SendEmailPoison")]
    public Task Run(
        [QueueTrigger("send-email-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
