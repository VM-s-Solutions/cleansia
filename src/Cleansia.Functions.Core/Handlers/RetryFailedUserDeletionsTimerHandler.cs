using Cleansia.Core.AppServices.Features.Gdpr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Daily, re-run the erasures that did not complete. A row that is still Failed after its retry is logged
/// at Error by the sweep itself, per request — until admin notifications exist that line is the alarm.
/// </summary>
public class RetryFailedUserDeletionsTimerHandler(
    IMediator mediator,
    ILogger<RetryFailedUserDeletionsTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("RetryFailedUserDeletions timer triggered at {Time}", DateTime.UtcNow);

        var result = await mediator.Send(new RetryFailedUserDeletions.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "RetryFailedUserDeletions completed; {Candidates} due, {Completed} erased, {Failed} still failed",
                result.Value.Candidates,
                result.Value.Completed,
                result.Value.Failed);
        }
        else
        {
            logger.LogError(
                "RetryFailedUserDeletions failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
