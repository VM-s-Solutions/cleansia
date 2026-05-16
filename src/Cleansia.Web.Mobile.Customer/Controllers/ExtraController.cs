using Cleansia.Core.AppServices.Features.Extras;
using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExtraController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [AllowAnonymous]
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<ExtraListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<ExtraListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetExtraOverview.Request(), cancellationToken);
    }
}
