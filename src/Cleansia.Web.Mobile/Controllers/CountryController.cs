using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Web.Mobile.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CountryController(IMediator mediator) : MobileApiController(mediator)
{
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<CountryListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CountryListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCountryOverview.Request(), cancellationToken);
    }
}
