using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.AdminUsers;
using Cleansia.Core.AppServices.Features.AdminUsers.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminUserController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("get-paged")]
    [Permission(Policy.CanViewAdminUsers)]
    [ProducesResponseType(typeof(PagedData<AdminUserListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedAdminUsers(
        [FromBody] GetPagedAdminUsers.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{userId}")]
    [Permission(Policy.CanViewAdminUsers)]
    [ProducesResponseType(typeof(AdminUserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminUserById(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetAdminUserById.Query(userId), cancellationToken);
        return HandleResult<AdminUserDetailDto>(result);
    }

    [HttpPost]
    [Permission(Policy.CanCreateAdminUser)]
    [ProducesResponseType(typeof(CreateAdminUser.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAdminUser(
        [FromBody] CreateAdminUser.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateAdminUser.Response>(result);
    }

    [HttpPut("{userId}")]
    [Permission(Policy.CanUpdateAdminUser)]
    [ProducesResponseType(typeof(UpdateAdminUser.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAdminUser(
        string userId,
        [FromBody] UpdateAdminUser.Command command,
        CancellationToken cancellationToken)
    {
        if (command.UserId != userId)
        {
            return BadRequest("User ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateAdminUser.Response>(result);
    }

    [HttpPost("{userId}/deactivate")]
    [Permission(Policy.CanDeactivateAdminUser)]
    [ProducesResponseType(typeof(DeactivateAdminUser.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAdminUser(
        string userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new DeactivateAdminUser.Command(userId, currentUserId), cancellationToken);
        return HandleResult<DeactivateAdminUser.Response>(result);
    }

    [HttpPost("{userId}/activate")]
    [Permission(Policy.CanActivateAdminUser)]
    [ProducesResponseType(typeof(ActivateAdminUser.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateAdminUser(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivateAdminUser.Command(userId), cancellationToken);
        return HandleResult<ActivateAdminUser.Response>(result);
    }
}