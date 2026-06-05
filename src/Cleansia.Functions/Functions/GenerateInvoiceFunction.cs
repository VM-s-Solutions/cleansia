using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in GenerateInvoiceHandler (Core).
public class GenerateInvoiceFunction(GenerateInvoiceHandler handler)
{
    [Function("GenerateInvoice")]
    public Task Run(
        [QueueTrigger("generate-invoice", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
