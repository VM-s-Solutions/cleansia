using Cleansia.Core.AppServices.Features.Bookings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Daily at 02:30 UTC, fire <c>recurring.scheduled</c> push notifications
/// to customers whose materialized recurring-booking Orders are due in
/// roughly the next 24h. Runs 30min after
/// <c>MaterializeRecurringBookingsHandler</c> so newly-spawned
/// orders for tomorrow are eligible immediately.
///
/// The handler stamps <c>Order.RecurringReminderSentAt</c> on success so
/// successive sweeps within the 24h window don't duplicate-send.
/// </summary>
public class SendRecurringOrderRemindersHandler(
    IMediator mediator,
    ILogger<SendRecurringOrderRemindersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
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
