using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D3 (F3) — thin -poison trigger shell; body lives in CompanyArchivePoisonHandler (Core).
public class CompanyArchivePoisonFunction(CompanyArchivePoisonHandler handler)
{
    [Function("CompanyArchivePoison")]
    public Task Run(
        [QueueTrigger("company-archive-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
