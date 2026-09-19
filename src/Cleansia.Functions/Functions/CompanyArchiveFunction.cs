using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in CompanyArchiveHandler (Core).
public class CompanyArchiveFunction(CompanyArchiveHandler handler)
{
    [Function("CompanyArchive")]
    public Task Run(
        [QueueTrigger("company-archive", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
