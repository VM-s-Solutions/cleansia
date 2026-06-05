using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0120 / ADR-0002 D3 (F3) — thin -poison trigger shell. The Worker SDK source-gen discovers the
// [QueueTrigger("generate-receipt-poison")] here in the Exe; the testable body (record + alert + ack,
// never re-process) lives in GenerateReceiptPoisonHandler (Core).
public class GenerateReceiptPoisonFunction(GenerateReceiptPoisonHandler handler)
{
    [Function("GenerateReceiptPoison")]
    public Task Run(
        [QueueTrigger("generate-receipt-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
