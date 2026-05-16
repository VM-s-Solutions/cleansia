using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServiceController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [AllowAnonymous]
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<ServiceListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<ServiceListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServiceOverview.Request(), cancellationToken);
    }
}
