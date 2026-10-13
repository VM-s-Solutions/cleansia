using Cleansia.Core.AppServices.Features.Bookings;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>Hourly at :30 UTC, remind unconfirmed recurring occurrences 2–26 hours ahead once.
/// A failed occurrence keeps its own scope and is retried on the next tick.</summary>
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
                "SendRecurringOrderReminders completed; sent {Sent} of {Considered} reminders; failed {Failed}",
                result.Value.RemindersSent,
                result.Value.Considered,
                result.Value.Failed);
        }
        else
        {
            logger.LogError(
                "SendRecurringOrderReminders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
