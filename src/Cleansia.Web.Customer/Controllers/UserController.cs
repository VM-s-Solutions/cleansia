using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("GetCurrent")]
    [Permission(Policy.CanGetCurrentUser)]
    [ProducesResponseType(typeof(MyProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser([FromQuery] GetCurrentUser.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<MyProfileDto>(result);
    }

    [HttpPut("UpdateCurrentUser")]
    [Permission(Policy.CanUpdateCurrentUser)]
    [ProducesResponseType(typeof(UpdateCurrentUser.Response), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateCurrentUser.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<UpdateCurrentUser.Response>(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPut("RequestPasswordChange")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPasswordChange([FromBody] RequestPasswordChange.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<UserListItem>(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPut("ChangePassword")]
    [ProducesResponseType(typeof(ChangePassword.Response), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePassword.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<ChangePassword.Response>(result);
    }
}
