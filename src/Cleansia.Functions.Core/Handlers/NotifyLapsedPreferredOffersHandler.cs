using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every five minutes, tell customers whose favourite cleaner did not answer that the offer closed and
/// they may name a second one. The sweep ANNOUNCES only — the reservation was released by the clock,
/// so if this handler never runs the order is still on the open board on time and the only cost is a
/// missing prompt.
///
/// <para>Five minutes rather than fifteen because the shortest reservation the policy can produce is 48
/// minutes, and a fifteen-minute sweep would add up to 31% to it before the customer heard anything.
/// The <c>Order.PreferredOfferLapseNotifiedAt</c> per-entity receipt is the duplicate-suppression
/// mechanism, so overlapping chances cost nothing.</para>
/// </summary>
public class NotifyLapsedPreferredOffersHandler(
    IMediator mediator,
    ILogger<NotifyLapsedPreferredOffersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("NotifyLapsedPreferredOffers timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new NotifyLapsedPreferredOffers.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "NotifyLapsedPreferredOffers completed; announced {Notified} of {Considered} lapsed reservations",
                result.Value.NotifiedCount,
                result.Value.Considered);
        }
        else
        {
            logger.LogError(
                "NotifyLapsedPreferredOffers failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
