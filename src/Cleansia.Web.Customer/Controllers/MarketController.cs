using Cleansia.Core.AppServices.Features.Markets;
using Cleansia.Core.AppServices.Features.Markets.DTOs;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

/// <summary>
/// The markets a customer may browse in — a serviced country joined to its active currency, the
/// default one flagged, and the per-market copy figures (ADR-0058). One anonymous call gives a
/// pre-address surface everything it needs; the landing page reads it before anyone signs in.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class MarketController(IMediator mediator) : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IReadOnlyList<MarketListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IReadOnlyList<MarketListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetMarkets.Request(), cancellationToken);
    }
}
