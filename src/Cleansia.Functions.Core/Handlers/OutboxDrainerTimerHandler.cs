using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// The testable timer-handler body for the single outbox drainer. The [TimerTrigger] shell stays in
/// the Exe; this Core body drives one drain tick per wakeup so Cleansia.Tests can reference it. This is
/// the ONE place the drainer runs — the Functions host keeps the post-commit dispatch behavior (so an
/// in-Function command still writes a durable row) but does not drain per instance.
/// </summary>
public class OutboxDrainerTimerHandler(
    IOutboxDrainerService drainerService,
    ILogger<OutboxDrainerTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var dispatched = await drainerService.DrainOnceAsync(ct);
        if (dispatched > 0)
        {
            logger.LogInformation("Outbox drainer dispatched {Count} messages", dispatched);
        }
    }
}
