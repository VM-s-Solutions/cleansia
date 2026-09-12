using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

/// <summary>
/// The currencies the platform knows, with the one it quotes in flagged <c>isDefault</c>. The
/// catalogue overviews are priced in that currency and carry no code of their own, so this is how a
/// customer surface learns what to label a catalogue price with -- and, later, what it may choose
/// from. Anonymous like the catalogue it labels: a visitor sees prices before signing in (T-0706).
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class CurrencyController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [AllowAnonymous]
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<CurrencyListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CurrencyListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCurrencyOverview.Request(), cancellationToken);
    }
}
