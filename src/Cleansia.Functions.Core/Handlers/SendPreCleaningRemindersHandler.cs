using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every five minutes, send the customer the "your cleaning starts in about an hour" push the
/// booking-confirmation screen promises. The sweep's window is fifteen minutes wide, so this cadence
/// gives every qualifying order at least one — usually three — chances to be caught inside it.
///
/// <para>The <c>Order.PreCleaningReminderSentAt</c> per-entity stamp is the duplicate-suppression
/// mechanism, so the overlapping chances cost nothing.</para>
/// </summary>
public class SendPreCleaningRemindersHandler(
    IMediator mediator,
    ILogger<SendPreCleaningRemindersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("SendPreCleaningReminders timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new SendPreCleaningReminders.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "SendPreCleaningReminders completed; sent {Sent} of {Considered} reminders",
                result.Value.RemindersSent,
                result.Value.Considered);
        }
        else
        {
            logger.LogError(
                "SendPreCleaningReminders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
