using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// The single dedicated outbox drainer host. A timer trigger is a singleton by construction (it holds a
// schedule lease, so exactly one instance fires per tick), which is the "exactly one drainer" the host
// decision requires; the row-level claim lease is the safety net if that is briefly violated during a
// deploy overlap. The Functions host keeps the post-commit dispatch behavior (in-Function commands
// write durable rows) but this is the only place the rows are drained.
public class OutboxDrainerFunction(OutboxDrainerTimerHandler handler)
{
    [Function("OutboxDrainer")]
    public Task Run([TimerTrigger("*/10 * * * * *")] TimerInfo timer, CancellationToken ct)
        => handler.HandleAsync(ct);
}
