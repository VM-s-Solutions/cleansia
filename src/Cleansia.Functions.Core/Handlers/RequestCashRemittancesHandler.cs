using Cleansia.Core.AppServices.Features.CashHeld;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Once a day, e-mail each cleaner whose cash has been carried past a pay-period close for longer than their
/// company allows a request to hand it over.
/// </summary>
public class RequestCashRemittancesHandler(
    IMediator mediator,
    ILogger<RequestCashRemittancesHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new RequestCashRemittances.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            if (result.Value.Requested > 0)
            {
                logger.LogInformation("RequestCashRemittances asked {Requested} cleaner(s) to hand over cash", result.Value.Requested);
            }
        }
        else
        {
            logger.LogError("RequestCashRemittances failed: {Error}", result.Error?.Message ?? "unknown");
        }
    }
}
