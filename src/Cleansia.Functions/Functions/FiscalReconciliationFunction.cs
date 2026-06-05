using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D3.4 — thin trigger shell; the testable body lives in
// FiscalReconciliationTimerHandler (Core). Sibling to RetryFailedFiscalRegistrationsFunction, but a
// DISTINCT layer: this re-enqueues the never-dispatched fiscal MESSAGE (the at-most-once Wave-0 gap);
// the retry function re-registers an already-claimed receipt with the authority.
public class FiscalReconciliationFunction(FiscalReconciliationTimerHandler handler)
{
    // Runs every 5 minutes; the per-item threshold (default 15 min, configurable) decides what is
    // actually swept, so frequent wakeups are cheap and a re-enqueue that races a just-late dispatch is
    // harmlessly deduped downstream.
    [Function("FiscalReconciliation")]
    public Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
