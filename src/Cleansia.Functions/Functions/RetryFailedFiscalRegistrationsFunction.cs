using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in RetryFailedFiscalRegistrationsHandler (Core).
public class RetryFailedFiscalRegistrationsFunction(RetryFailedFiscalRegistrationsHandler handler)
{
    // Runs every 5 minutes. The service itself enforces an exponential-backoff schedule
    // (1m, 2m, 5m, 15m, 1h, 6h, 24h) via FiscalNextRetryAt, so frequent wakeups are cheap.
    [Function("RetryFailedFiscalRegistrations")]
    public Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
