using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.CompanyLifecycle;
using Cleansia.Core.AppServices.Features.CompanyLifecycle.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

/// <summary>
/// The admin's own operating company's lifecycle (ADR-0064): where it stands, and the acts that move
/// it. The company is the caller's tenant claim; there is no way to name another.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AdminCompanyLifecycleController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get")]
    [Permission(Policy.CanViewCompanyLifecycle)]
    [ProducesResponseType(typeof(CompanyLifecycleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCompanyLifecycle.Query(), cancellationToken);
        return HandleResult<CompanyLifecycleDto>(result);
    }

    [HttpPost("deactivate")]
    [Permission(Policy.CanDeactivateCompany)]
    [ProducesResponseType(typeof(DeactivateCompany.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Deactivate(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateCompany.Command(), cancellationToken);
        return HandleResult<DeactivateCompany.Response>(result);
    }

    [HttpPost("reactivate")]
    [Permission(Policy.CanReactivateCompany)]
    [ProducesResponseType(typeof(ReactivateCompany.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Reactivate(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ReactivateCompany.Command(), cancellationToken);
        return HandleResult<ReactivateCompany.Response>(result);
    }
}
