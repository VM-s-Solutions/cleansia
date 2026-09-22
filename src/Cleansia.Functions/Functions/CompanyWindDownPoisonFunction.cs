using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D3 (F3) — thin -poison trigger shell; body lives in CompanyWindDownPoisonHandler (Core).
public class CompanyWindDownPoisonFunction(CompanyWindDownPoisonHandler handler)
{
    [Function("CompanyWindDownPoison")]
    public Task Run(
        [QueueTrigger("company-wind-down-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
