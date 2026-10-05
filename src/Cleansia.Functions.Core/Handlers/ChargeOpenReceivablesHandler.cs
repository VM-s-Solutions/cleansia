using Cleansia.Core.AppServices.Features.Receivables;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Every fifteen minutes, charge each new open receivable once to the customer's saved card. A no-op while
/// <c>Payments:OffSessionChargesEnabled</c> is off, and it stays off: switching it on would contradict the
/// owner's ruling of 2026-10-04 that no saved card is charged.
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
