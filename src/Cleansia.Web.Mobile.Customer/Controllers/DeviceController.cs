using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.AppServices.Features.Devices.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DeviceController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpPost("Register")]
    [Permission(Policy.Authenticated)]
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
    [ProducesResponseType(typeof(UnregisterDevice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unregister([FromQuery] UnregisterDevice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UnregisterDevice.Response>(result);
    }
}
