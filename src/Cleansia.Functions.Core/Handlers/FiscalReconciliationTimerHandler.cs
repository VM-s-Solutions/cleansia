using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

// T-0122 (FISCAL-RECON) / ADR-0002 D3.4 — the testable timer-handler body for the dispatch
// reconciliation sweep. The [TimerTrigger] shell stays in the Exe (sibling to
// RetryFailedFiscalRegistrations) per the T-0121 pattern; this Core body drives the sweep once per tick
// so Cleansia.Tests can reference it.
public class FiscalReconciliationTimerHandler(
    IFiscalReconciliationService reconciliationService,
    ILogger<FiscalReconciliationTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("FiscalReconciliation timer triggered at {Time}", DateTime.UtcNow);
        var reEnqueued = await reconciliationService.ReconcileAsync(ct);
        logger.LogInformation("FiscalReconciliation re-enqueued {Count} messages", reEnqueued);
    }
}
