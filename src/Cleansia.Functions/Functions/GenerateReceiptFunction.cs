using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell. The Worker SDK source-gen discovers the
// [Function] trigger here in the Exe; the testable body lives in GenerateReceiptHandler (Core).
public class GenerateReceiptFunction(GenerateReceiptHandler handler)
{
    [Function("GenerateReceipt")]
    public Task Run(
        [QueueTrigger("generate-receipt", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
