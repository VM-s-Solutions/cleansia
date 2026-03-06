using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PackageController(IMediator mediator) : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<PackageListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<PackageListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetPackageOverview.Request(), cancellationToken);
    }
}
