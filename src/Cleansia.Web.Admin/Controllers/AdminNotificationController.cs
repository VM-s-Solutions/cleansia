using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

/// <summary>
/// The administrator's own notification feed. The audience is set here, never read from the client,
/// so the console can only ever see, count and mark the caller's <c>admin.*</c> rows.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AdminNotificationController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewAdminNotifications)]
    [ProducesResponseType(typeof(PagedData<UserNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<UserNotificationDto>> GetPaged(
        [FromQuery] GetPagedUserNotifications.Request request, CancellationToken cancellationToken)
    {
        request.Audience = NotificationFeedAudience.Admin;
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("unread-count")]
    [Permission(Policy.CanViewAdminNotifications)]
    [ProducesResponseType(typeof(UnreadNotificationCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetUnreadNotificationCount.Query(NotificationFeedAudience.Admin), cancellationToken);
        return HandleResult<UnreadNotificationCountDto>(result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("mark-read")]
    [Permission(Policy.CanViewAdminNotifications)]
    [ProducesResponseType(typeof(MarkNotificationRead.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkRead(
        [FromBody] MarkNotificationRead.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            command with { Audience = NotificationFeedAudience.Admin }, cancellationToken);
        return HandleResult<MarkNotificationRead.Response>(result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("mark-all-read")]
    [Permission(Policy.CanViewAdminNotifications)]
    [ProducesResponseType(typeof(MarkAllNotificationsRead.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAllRead(
        [FromBody] MarkAllNotificationsRead.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            command with { Audience = NotificationFeedAudience.Admin }, cancellationToken);
        return HandleResult<MarkAllNotificationsRead.Response>(result);
    }
}
