using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Features.Users.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Abstractions;
using Cleansia.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetPaged")]
    [Permission(Policy.CanViewPagedUser)]
    [ProducesResponseType(typeof(PagedData<UserListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<UserListItem>> GetPaged([FromQuery] GetPagedUsers.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetById")]
    [Permission(Policy.CanViewUserDetail)]
    [ProducesResponseType(typeof(UserItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById([FromQuery] GetUser.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<UserItem>(result);
    }

    [HttpGet("GetCurrent")]
    [Permission(Policy.CanGetCurrentUser)]
    [ProducesResponseType(typeof(UserListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser([FromQuery] GetCurrentUser.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<UserListItem>(result);
    }

    [AllowAnonymous]
    [HttpPut("RequestPasswordChange")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPasswordChange([FromBody] RequestPasswordChange.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<UserListItem>(result);
    }

    [AllowAnonymous]
    [HttpPut("ChangePassword")]
    [ProducesResponseType(typeof(ChangePassword.Response), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePassword.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<ChangePassword.Response>(result);
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
}
