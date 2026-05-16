using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Bookings.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RecurringBookingController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("GetMine")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(typeof(IReadOnlyList<RecurringBookingTemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyRecurringBookings.Query(), cancellationToken);
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
        var result = await Mediator.Send(command, cancellationToken);
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
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RecurringBookingTemplateDto>(result);
    }

    [HttpPost("SetActive")]
    [Permission(Policy.CanManageRecurringBookings)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetActive(
        [FromBody] SetRecurringBookingActive.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
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
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<bool>(result);
    }
}
