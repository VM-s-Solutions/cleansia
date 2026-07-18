using Microsoft.AspNetCore.RateLimiting;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Notifications;
using Cleansia.Core.AppServices.Features.Notifications.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NotificationController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpGet("Paged")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(PagedData<UserNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<PagedData<UserNotificationDto>> GetPaged(
        [FromQuery] GetPagedUserNotifications.Request request, CancellationToken cancellationToken)
    {
        request.Audience = NotificationFeedAudience.Customer;
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("UnreadCount")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(UnreadNotificationCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetUnreadNotificationCount.Query(NotificationFeedAudience.Customer), cancellationToken);
        return HandleResult<UnreadNotificationCountDto>(result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("MarkRead")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(MarkNotificationRead.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkRead(
        [FromBody] MarkNotificationRead.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            command with { Audience = NotificationFeedAudience.Customer }, cancellationToken);
        return HandleResult<MarkNotificationRead.Response>(result);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("MarkAllRead")]
    [Permission(Policy.Authenticated)]
    [ProducesResponseType(typeof(MarkAllNotificationsRead.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead(
        [FromBody] MarkAllNotificationsRead.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            command with { Audience = NotificationFeedAudience.Customer }, cancellationToken);
        return HandleResult<MarkAllNotificationsRead.Response>(result);
    }
}
