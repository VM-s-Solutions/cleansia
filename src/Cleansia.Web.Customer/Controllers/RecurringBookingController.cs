using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

/// <summary>
/// Recurring booking template endpoints — the Plus "schedule a weekly cleaning"
/// CRUD surface. All routes are scoped to the calling user via JWT-enriched
/// UserId; clients never set it. Same role gate as saved addresses + memberships.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class RecurringBookingController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("GetMine")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringBookingTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new GetMyRecurringBookings.Query(userId), cancellationToken);
        return HandleResult<IReadOnlyList<RecurringBookingTemplateDto>>(result);
    }

    [HttpPost("Create")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(typeof(RecurringBookingTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecurringBooking.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<RecurringBookingTemplateDto>(result);
    }

    [HttpPost("Update")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(typeof(RecurringBookingTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateRecurringBooking.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<RecurringBookingTemplateDto>(result);
    }

    /// <summary>
    /// Pause / resume — sets the IsActive flag. Pausing skips the materializer;
    /// already-spawned future Order rows are unaffected.
    /// </summary>
    [HttpPost("SetActive")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        [FromBody] SetRecurringBookingActive.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<bool>(result);
    }

    [HttpPost("Delete")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        [FromBody] DeleteRecurringBooking.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<bool>(result);
    }
}
