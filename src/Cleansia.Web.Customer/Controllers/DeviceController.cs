using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DeviceController(IMediator mediator) : CustomerApiController(mediator)
{
    /// <summary>
    /// Register or refresh a device for FCM push delivery. Idempotent on
    /// (UserId, DeviceId) — the same device id with a new token does an
    /// in-place token rotation.
    /// </summary>
    [HttpPost("Register")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(RegisterDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDevice.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RegisterDevice.Response>(result);
    }

    /// <summary>
    /// Remove the device row entirely. Called on user-initiated sign-out so
    /// the next user on this handset doesn't inherit the previous user's
    /// notifications.
    /// </summary>
    [HttpDelete("Unregister")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(UnregisterDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unregister(
        [FromQuery] UnregisterDevice.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UnregisterDevice.Response>(result);
    }
}
