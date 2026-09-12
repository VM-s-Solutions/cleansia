using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Extras;
using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminExtraController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewExtras)]
    [ProducesResponseType(typeof(PagedData<ExtraListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedExtras(
        [FromQuery] GetPagedExtras.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{extraId}")]
    [Permission(Policy.CanViewExtras)]
    [ProducesResponseType(typeof(AdminExtraDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExtraById(string extraId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetExtraById.Query(extraId), cancellationToken);
        return HandleResult<AdminExtraDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreateExtra)]
    [ProducesResponseType(typeof(CreateExtra.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> CreateExtra(
        [FromBody] CreateExtra.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateExtra.Response>(result);
    }

    [HttpPut("update/{extraId}")]
    [Permission(Policy.CanUpdateExtra)]
    [ProducesResponseType(typeof(UpdateExtra.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> UpdateExtra(
        string extraId,
        [FromBody] UpdateExtra.Command command,
        CancellationToken cancellationToken)
    {
        if (command.ExtraId != extraId)
        {
            return BadRequest("Extra ID in route does not match command");
        }

        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateExtra.Response>(result);
    }

    [HttpPost("deactivate/{extraId}")]
    [Permission(Policy.CanUpdateExtra)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeactivateExtra.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateExtra(
        string extraId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateExtra.Command(extraId), cancellationToken);
        return HandleResult<DeactivateExtra.Response>(result);
    }

    [HttpPost("activate/{extraId}")]
    [Permission(Policy.CanUpdateExtra)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ActivateExtra.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateExtra(
        string extraId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivateExtra.Command(extraId), cancellationToken);
        return HandleResult<ActivateExtra.Response>(result);
    }

    [HttpDelete("delete/{extraId}")]
    [Permission(Policy.CanDeleteExtra)]
    [ProducesResponseType(typeof(DeleteExtra.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteExtra(
        string extraId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteExtra.Command(extraId), cancellationToken);
        return HandleResult<DeleteExtra.Response>(result);
    }
}
