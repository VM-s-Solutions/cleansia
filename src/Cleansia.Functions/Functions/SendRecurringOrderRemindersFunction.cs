using Cleansia.Core.AppServices.Features.Bookings;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

/// <summary>
/// Daily at 02:30 UTC, fire <c>recurring.scheduled</c> push notifications
/// to customers whose materialized recurring-booking Orders are due in
/// roughly the next 24h. Runs 30min after
/// <see cref="MaterializeRecurringBookingsFunction"/> so newly-spawned
/// orders for tomorrow are eligible immediately.
///
/// The handler stamps <c>Order.RecurringReminderSentAt</c> on success so
/// successive sweeps within the 24h window don't duplicate-send.
/// </summary>
public class SendRecurringOrderRemindersFunction(
    IMediator mediator,
    ILogger<SendRecurringOrderRemindersFunction> logger)
{
    [Function("SendRecurringOrderReminders")]
    public async Task Run([TimerTrigger("0 */2 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("SendRecurringOrderReminders timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new SendRecurringOrderReminders.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "SendRecurringOrderReminders completed; sent {Sent} of {Considered} reminders",
                result.Value.RemindersSent,
                result.Value.Considered);
        }
        else
        {
            logger.LogError(
                "SendRecurringOrderReminders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
