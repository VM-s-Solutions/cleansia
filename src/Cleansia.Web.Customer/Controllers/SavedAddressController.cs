using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SavedAddressController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("GetMine")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(typeof(IReadOnlyList<SavedAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new GetSavedAddresses.Query(userId), cancellationToken);
        return HandleResult<IReadOnlyList<SavedAddressDto>>(result);
    }

    [HttpPost("Add")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(typeof(SavedAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Add([FromBody] AddSavedAddress.Command command, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<SavedAddressDto>(result);
    }

    [HttpPost("SetDefault")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetDefault([FromBody] SetDefaultSavedAddress.Command command, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<bool>(result);
    }

    [HttpPut("Update")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(typeof(SavedAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update([FromBody] UpdateSavedAddress.Command command, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<SavedAddressDto>(result);
    }

    [HttpDelete("Delete/{id}")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new DeleteSavedAddress.Command(id, userId), cancellationToken);
        return HandleResult<bool>(result);
    }
}
