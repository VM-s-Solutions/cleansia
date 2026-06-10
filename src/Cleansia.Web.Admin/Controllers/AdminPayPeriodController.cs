using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminPayPeriodController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPayPeriods)]
    [ProducesResponseType(typeof(PagedData<PayPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedPayPeriods([FromQuery] GetPagedPayPeriods.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{payPeriodId}")]
    [Permission(Policy.CanViewPayPeriod)]
    [ProducesResponseType(typeof(PayPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPayPeriod(string payPeriodId, CancellationToken cancellationToken)
    {
        var query = new GetPayPeriodById.Query(payPeriodId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<PayPeriodDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreatePayPeriod)]
    [ProducesResponseType(typeof(CreatePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePayPeriod([FromBody] CreatePayPeriod.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreatePayPeriod.Response>(result);
    }

    [HttpPut("update")]
    [Permission(Policy.CanUpdatePayPeriod)]
    [ProducesResponseType(typeof(UpdatePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePayPeriod([FromBody] UpdatePayPeriod.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdatePayPeriod.Response>(result);
    }

    [HttpDelete("delete/{payPeriodId}")]
    [Permission(Policy.CanDeletePayPeriod)]
    [ProducesResponseType(typeof(DeletePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePayPeriod(string payPeriodId, CancellationToken cancellationToken)
    {
        var command = new DeletePayPeriod.Command(payPeriodId);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<DeletePayPeriod.Response>(result);
    }

    [HttpPost("open")]
    [Permission(Policy.CanOpenPayPeriod)]
    [ProducesResponseType(typeof(OpenPayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenPayPeriod([FromBody] OpenPayPeriod.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<OpenPayPeriod.Response>(result);
    }

    [HttpPost("close")]
    [Permission(Policy.CanClosePayPeriod)]
    [ProducesResponseType(typeof(ClosePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ClosePayPeriod([FromBody] ClosePayPeriod.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ClosePayPeriod.Response>(result);
    }

    [HttpPost("mark-paid")]
    [Permission(Policy.CanMarkPayPeriodPaid)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(MarkPayPeriodPaid.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkPayPeriodPaid([FromBody] MarkPayPeriodPaid.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<MarkPayPeriodPaid.Response>(result);
    }

    [HttpPost("reopen")]
    [Permission(Policy.CanReopenPayPeriod)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ReopenPayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReopenPayPeriod([FromBody] ReopenPayPeriod.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ReopenPayPeriod.Response>(result);
    }
}
