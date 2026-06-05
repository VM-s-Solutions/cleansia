using Cleansia.Core.AppServices.Features.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every 15 minutes, sweep card orders that have been sitting in Pending for
/// more than an hour and mark them Cancelled. Picks up users who opened
/// PaymentSheet on mobile and closed it without paying — without this they'd
/// stay visible to cleaners (matching pool pollution) until Stripe eventually
/// expires the underlying PaymentIntent ~24h later.
/// </summary>
public class CleanupStalePendingOrdersHandler(
    IMediator mediator,
    ILogger<CleanupStalePendingOrdersHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("CleanupStalePendingOrders timer triggered at {Time}", DateTime.UtcNow);
        var result = await mediator.Send(new CleanupStalePendingOrders.Command(OlderThanHours: 1), ct);
        if (result.IsSuccess && result.Value != null)
        {
            logger.LogInformation(
                "CleanupStalePendingOrders completed; cancelled {Count} orders",
                result.Value.CancelledCount);
        }
        else
        {
            logger.LogError(
                "CleanupStalePendingOrders failed: {Error}",
                result.Error?.Message ?? "unknown");
        }
    }
}
