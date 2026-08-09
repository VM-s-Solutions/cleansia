using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in NotifyLapsedPreferredOffersHandler (Core).
/// <summary>Every five minutes. Tells the customer their favourite cleaner's reservation closed without
/// a confirmation. Cadence precedent: <c>FiscalReconciliationFunction</c> /
/// <c>RetryFailedFiscalRegistrationsFunction</c>. It announces and releases nothing — the release is a
/// clock comparison in a WHERE clause — so a dead timer costs a prompt, never a booking.</summary>
public class NotifyLapsedPreferredOffersFunction(NotifyLapsedPreferredOffersHandler handler)
{
    [Function("NotifyLapsedPreferredOffers")]
    public Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
