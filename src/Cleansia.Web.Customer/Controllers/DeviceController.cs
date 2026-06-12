using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.AppServices.Features.Devices.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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

    /// <summary>
    /// List the caller's own registered devices. Scope is the JWT user (S1) —
    /// the optional currentDeviceId only flags which row is this handset.
    /// </summary>
    [HttpGet("Mine")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Mine(
        [FromQuery] string? currentDeviceId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyDevices.Query(currentDeviceId), cancellationToken);
        return HandleResult<IReadOnlyList<DeviceDto>>(result);
    }

    /// <summary>
    /// Revoke a device the caller owns by its server-side id: removes its push
    /// row and ends its session. A non-owned id is indistinguishable from a
    /// missing one (S3).
    /// </summary>
    [HttpDelete("{deviceRowId}")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RevokeDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(
        string deviceRowId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RevokeDevice.Command(deviceRowId), cancellationToken);
        return HandleResult<RevokeDevice.Response>(result);
    }
}
