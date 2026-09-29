using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// Thin trigger shell; body lives in RequestCashRemittancesHandler (Core).
public class RequestCashRemittancesFunction(RequestCashRemittancesHandler handler)
{
    [Function("RequestCashRemittances")]
    public Task Run([TimerTrigger("0 0 8 * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
