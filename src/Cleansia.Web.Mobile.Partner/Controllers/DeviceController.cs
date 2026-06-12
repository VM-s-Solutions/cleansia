using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.AppServices.Features.Devices.DTOs;
using Cleansia.Web.Mobile.Partner.Abstractions;
using Cleansia.Web.Mobile.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DeviceController(IMediator mediator) : MobileApiController(mediator)
{
    [HttpPost("Register")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RegisterDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] RegisterDevice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RegisterDevice.Response>(result);
    }

    [HttpDelete("Unregister")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UnregisterDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unregister([FromQuery] UnregisterDevice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UnregisterDevice.Response>(result);
    }

    [HttpGet("Mine")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Mine([FromQuery] string? currentDeviceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyDevices.Query(currentDeviceId), cancellationToken);
        return HandleResult<IReadOnlyList<DeviceDto>>(result);
    }

    [HttpDelete("{deviceRowId}")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RevokeDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Revoke(string deviceRowId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RevokeDevice.Command(deviceRowId), cancellationToken);
        return HandleResult<RevokeDevice.Response>(result);
    }
}
