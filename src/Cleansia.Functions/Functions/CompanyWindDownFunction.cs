using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in CompanyWindDownHandler (Core).
public class CompanyWindDownFunction(CompanyWindDownHandler handler)
{
    [Function("CompanyWindDown")]
    public Task Run(
        [QueueTrigger("company-wind-down", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
