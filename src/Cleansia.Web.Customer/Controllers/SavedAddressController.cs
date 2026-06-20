using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SavedAddressController(IMediator mediator) : CustomerSavedAddressControllerBase(mediator)
{
    [HttpGet("GetMine")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(typeof(IReadOnlyList<SavedAddressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> GetMine(CancellationToken cancellationToken)
        => GetMineCore(cancellationToken);

    [HttpPost("Add")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(typeof(SavedAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Add([FromBody] AddSavedAddress.Command command, CancellationToken cancellationToken)
        => AddCore(command, cancellationToken);

    [HttpPost("SetDefault")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> SetDefault([FromBody] SetDefaultSavedAddress.Command command, CancellationToken cancellationToken)
        => SetDefaultCore(command, cancellationToken);

    [HttpPut("Update")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(typeof(SavedAddressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Update([FromBody] UpdateSavedAddress.Command command, CancellationToken cancellationToken)
        => UpdateCore(command, cancellationToken);

    [HttpDelete("Delete/{id}")]
    [Permission(Policy.CanManageSavedAddresses)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
        => DeleteCore(id, cancellationToken);
}
