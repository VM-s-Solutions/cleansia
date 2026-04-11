using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

public class RetryFailedFiscalRegistrationsFunction(
    IFiscalRetryService fiscalRetryService,
    ILogger<RetryFailedFiscalRegistrationsFunction> logger)
{
    // Runs every 5 minutes. The service itself enforces an exponential-backoff schedule
    // (1m, 2m, 5m, 15m, 1h, 6h, 24h) via FiscalNextRetryAt, so frequent wakeups are cheap.
    [Function("RetryFailedFiscalRegistrations")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("RetryFailedFiscalRegistrations timer triggered at {Time}", DateTime.UtcNow);
        var processed = await fiscalRetryService.ProcessDueRetriesAsync(ct);
        logger.LogInformation("RetryFailedFiscalRegistrations processed {Count} receipts", processed);
    }
}
