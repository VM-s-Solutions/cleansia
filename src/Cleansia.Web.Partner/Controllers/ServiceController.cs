using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Web.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServiceController(IMediator mediator): ApiController(mediator)
{
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<ServiceListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<ServiceListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServiceOverview.Request(), cancellationToken);
    }
}