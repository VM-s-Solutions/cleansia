using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Refunds;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Hourly sweep that cancels Pending recurring-template Orders whose
/// confirmation window has lapsed. Frees the cleaner's slot when the
/// customer ignores the 24h-ahead reminder (sent by
/// <c>SendRecurringOrderRemindersHandler</c>) and never confirms.
///
/// Hourly cadence keeps the gap between "should have confirmed" and "slot
/// freed for someone else" short enough that it doesn't affect cleaner
/// scheduling expectations. Idempotent: a cancelled order's PaymentStatus
/// stops matching the sweep's filter, so re-running mid-hour is a no-op.
///
/// <para>The same tick re-drives pending card refunds (<c>RedrivePendingRefunds</c>).</para>
/// </summary>
public class AutoCancelStaleRecurringOrdersHandler(
    IMediator mediator,
    ILogger<AutoCancelStaleRecurringOrdersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
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

        // A SECOND command on this hourly schedule rather than a timer of its own (the argument
        // CleanupStalePendingOrdersHandler records), sent separately so neither failure hides the other.
        var refunds = await mediator.Send(new RedrivePendingRefunds.Command(), ct);
        if (refunds.IsSuccess && refunds.Value != null)
        {
            logger.LogInformation(
                "RedrivePendingRefunds completed; re-drove {Redriven} of {Considered}, alerted {Alerted}",
                refunds.Value.Redriven,
                refunds.Value.Considered,
                refunds.Value.Alerted);
        }
        else
        {
            logger.LogError(
                "RedrivePendingRefunds failed: {Error}",
                refunds.Error?.Message ?? "unknown");
        }
    }
}
