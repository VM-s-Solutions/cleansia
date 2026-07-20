using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.LiveActivities;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LiveActivityController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpPost("Register")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RegisterLiveActivityToken.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] RegisterLiveActivityToken.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RegisterLiveActivityToken.Response>(result);
    }

    [HttpDelete("{orderId}")]
    [Permission(Policy.Authenticated)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UnregisterLiveActivity.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unregister(string orderId, [FromQuery] string deviceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UnregisterLiveActivity.Command(orderId, deviceId), cancellationToken);
        return HandleResult<UnregisterLiveActivity.Response>(result);
    }
}
