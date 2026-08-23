using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Hourly, on the hour: tell each cleaner how many jobs they have tomorrow, at 18:00 in their own
/// local time.
///
/// <para><b>The hourly cadence is the mechanism, not a choice.</b> An Azure timer fires on a UTC cron
/// and cannot be timezone-aware, so the sweep runs every hour and selects the cleaners whose local
/// clock has just struck the send hour. A daily tick could only ever be right for one timezone.</para>
///
/// <para>The per-cleaner <c>Employee.LastTomorrowDigestAt</c> watermark, compared in the cleaner's own
/// zone, is what stops a second digest for the same local evening.</para>
/// </summary>
public class SendTomorrowJobDigestHandler(
    IMediator mediator,
    ILogger<SendTomorrowJobDigestHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("SendTomorrowJobDigest timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new SendTomorrowJobDigest.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "SendTomorrowJobDigest completed; sent {Sent} digests of {Considered} cleaners",
                result.Value.DigestsSent,
                result.Value.CleanersConsidered);
        }
        else
        {
            logger.LogError(
                "SendTomorrowJobDigest failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
