using Cleansia.Core.AppServices.Features.Markets;
using Cleansia.Core.AppServices.Features.Markets.DTOs;
using Cleansia.Web.Mobile.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Partner.Controllers;

/// <summary>
/// The markets a cleaner may register into — a serviced country joined to its active currency, the
/// default one flagged (ADR-0058). The partner register form reads it anonymously so a cleaner
/// picks the operating company that will employ them (ADR-0061 D6); the same query the Customer
/// host serves, so both directories always agree.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class MarketController(IMediator mediator) : MobileApiController(mediator)
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
