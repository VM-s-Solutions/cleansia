using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// ADR-0002 D5 step 1 — thin trigger shell; body lives in ExpireStaleCreditHandler (Core).
public class ExpireStaleCreditFunction(ExpireStaleCreditHandler handler)
{
    /// 03:30 daily. Beside LiveActivityJanitor's 04:00 and the weekly retention sweep's 03:00,
    /// deliberately offset from both so three janitors do not open transactions on the same minute.
    [Function("ExpireStaleCredit")]
    public Task Run([TimerTrigger("0 30 3 * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
