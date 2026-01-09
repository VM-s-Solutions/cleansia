using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Web.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CurrencyController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<CurrencyListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CurrencyListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCurrencyOverview.Request(), cancellationToken);
    }
}