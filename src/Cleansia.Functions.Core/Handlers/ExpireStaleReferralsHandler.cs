using Cleansia.Core.AppServices.Features.Referrals;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Recurring sweep that flips Accepted referrals past the 90-day qualifying window to Expired.
/// Dispatches the <see cref="ExpireStaleReferrals.Command"/> so the domain transition commits through
/// the UoW MediatR pipeline (ADR-0002 D1.3 — the Functions host runs the full pipeline). The
/// GetExpirableAsync cutoff filter is the dedup: a re-run never re-expires an already-terminal row, so
/// over-firing is harmless.
/// </summary>
public class ExpireStaleReferralsHandler(
    IMediator mediator,
    ILogger<ExpireStaleReferralsHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation(
            "ExpireStaleReferrals timer triggered at {Time}",
            DateTime.UtcNow);
        var result = await mediator.Send(new ExpireStaleReferrals.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "ExpireStaleReferrals completed; expired {ExpiredCount} stale referrals",
                result.Value.ExpiredCount);
        }
        else
        {
            logger.LogError(
                "ExpireStaleReferrals failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
