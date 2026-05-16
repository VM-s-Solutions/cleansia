using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationPreferencesController(IMediator mediator)
    : CustomerMobileApiController(mediator)
{
    /// <summary>
    /// Returns the calling user's per-category push preferences. Lazy-creates
    /// the row with defaults on first call so subsequent PUTs always have
    /// something to patch.
    /// </summary>
    [HttpGet("GetMine")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetMyNotificationPreferences.Command(),
            cancellationToken);
        return HandleResult<NotificationPreferencesDto>(result);
    }

    [HttpPut("Update")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateNotificationPreferences.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<NotificationPreferencesDto>(result);
    }
}
