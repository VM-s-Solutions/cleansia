using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every five minutes, remind a cleaner that a job they took starts in about two hours, and nudge them
/// close to the start if they still have not set off.
///
/// <para>The cadence is bound to the sweeps' windows, not chosen freely: both are wider than five
/// minutes, so every assignment gets several chances to be caught inside one. The per-assignment
/// <c>ReminderSoonSentAt</c> / <c>ReminderNotStartedSentAt</c> stamps are the duplicate-suppression
/// mechanism, so those overlapping chances cost nothing.</para>
/// </summary>
public class SendCleanerJobRemindersHandler(
    IMediator mediator,
    ILogger<SendCleanerJobRemindersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("SendCleanerJobReminders timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new SendCleanerJobReminders.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "SendCleanerJobReminders completed; {Soon} two-hour notices and {Nudges} nudges over {Considered} assignments",
                result.Value.SoonSent,
                result.Value.NudgesSent,
                result.Value.Considered);
        }
        else
        {
            logger.LogError(
                "SendCleanerJobReminders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
