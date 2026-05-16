using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminLoyaltyTierController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-all")]
    [Permission(Policy.CanViewLoyaltyTierConfigs)]
    [ProducesResponseType(typeof(GetAllTierConfigs.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllTierConfigs(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetAllTierConfigs.Query(), cancellationToken);
        return HandleResult<GetAllTierConfigs.Response>(result);
    }

    [HttpPut("update/{tierConfigId}")]
    [Permission(Policy.CanUpdateLoyaltyTierConfig)]
    [ProducesResponseType(typeof(UpdateTierConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTierConfig(
        string tierConfigId,
        [FromBody] UpdateTierConfig.Command command,
        CancellationToken cancellationToken)
    {
        if (command.TierConfigId != tierConfigId)
        {
            return BadRequest("Tier config ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateTierConfig.Response>(result);
    }

    [HttpPost("preview-threshold-impact")]
    [Permission(Policy.CanUpdateLoyaltyTierConfig)]
    [ProducesResponseType(typeof(PreviewTierThresholdImpact.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PreviewThresholdImpact(
        [FromBody] PreviewTierThresholdImpact.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<PreviewTierThresholdImpact.Response>(result);
    }
}
