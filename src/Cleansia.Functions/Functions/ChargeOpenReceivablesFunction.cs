using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// Thin trigger shell; body lives in ChargeOpenReceivablesHandler (Core).
public class ChargeOpenReceivablesFunction(ChargeOpenReceivablesHandler handler)
{
    [Function("ChargeOpenReceivables")]
    public Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
