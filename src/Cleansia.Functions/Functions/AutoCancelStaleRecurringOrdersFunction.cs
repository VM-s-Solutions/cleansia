using Cleansia.Core.AppServices.Features.Bookings;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

/// <summary>
/// Hourly sweep that cancels Pending recurring-template Orders whose
/// confirmation window has lapsed. Frees the cleaner's slot when the
/// customer ignores the 24h-ahead reminder (sent by
/// <see cref="SendRecurringOrderRemindersFunction"/>) and never confirms.
///
/// Hourly cadence keeps the gap between "should have confirmed" and "slot
/// freed for someone else" short enough that it doesn't affect cleaner
/// scheduling expectations. Idempotent: a cancelled order's PaymentStatus
/// stops matching the sweep's filter, so re-running mid-hour is a no-op.
/// </summary>
public class AutoCancelStaleRecurringOrdersFunction(
    IMediator mediator,
    ILogger<AutoCancelStaleRecurringOrdersFunction> logger)
{
    [Function("AutoCancelStaleRecurringOrders")]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("AutoCancelStaleRecurringOrders timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new AutoCancelStaleRecurringOrders.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "AutoCancelStaleRecurringOrders completed; cancelled {Cancelled} of {Considered}",
                result.Value.Cancelled,
                result.Value.Considered);
        }
        else
        {
            logger.LogError(
                "AutoCancelStaleRecurringOrders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
