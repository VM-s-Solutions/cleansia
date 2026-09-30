using Cleansia.Core.AppServices.Features.Receivables;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every fifteen minutes, charge each new open receivable once to the customer's saved card. A no-op while
/// <c>Payments:OffSessionChargesEnabled</c> is off, which is where it stays until the terms carry the
/// consent wording for the card guarantee.
/// </summary>
public class ChargeOpenReceivablesHandler(
    IMediator mediator,
    ILogger<ChargeOpenReceivablesHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new ChargeOpenReceivables.Command(), ct);
        if (result.IsSuccess && result.Value != null)
        {
            if (result.Value.Attempted > 0)
            {
                logger.LogInformation(
                    "ChargeOpenReceivables attempted {Attempted} receivable(s), charged {Charged}",
                    result.Value.Attempted, result.Value.Charged);
            }
        }
        else
        {
            logger.LogError("ChargeOpenReceivables failed: {Error}", result.Error?.Message ?? "unknown");
        }
    }
}
