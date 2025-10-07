using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Abstractions;
using Cleansia.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PayPeriodController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetPagedPayPeriods")]
    [Permission(Policy.CanViewPayPeriods)]
    [ProducesResponseType(typeof(PagedData<PayPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<PayPeriodDto>> GetPagedPayPeriods([FromQuery] GetPagedPayPeriods.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetPayPeriodById")]
    [Permission(Policy.CanViewPayPeriod)]
    [ProducesResponseType(typeof(PayPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPayPeriodById([FromQuery] GetPayPeriodById.Query query)
    {
        var result = await Mediator.Send(query);
        return HandleResult<PayPeriodDto>(result);
    }

    [HttpPost("CreatePayPeriod")]
    [Permission(Policy.CanCreatePayPeriod)]
    [ProducesResponseType(typeof(CreatePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePayPeriod([FromBody] CreatePayPeriod.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CreatePayPeriod.Response>(result);
    }

    [HttpPut("UpdatePayPeriod")]
    [Permission(Policy.CanUpdatePayPeriod)]
    [ProducesResponseType(typeof(UpdatePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePayPeriod([FromBody] UpdatePayPeriod.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdatePayPeriod.Response>(result);
    }

    [HttpDelete("DeletePayPeriod")]
    [Permission(Policy.CanDeletePayPeriod)]
    [ProducesResponseType(typeof(DeletePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePayPeriod([FromBody] DeletePayPeriod.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<DeletePayPeriod.Response>(result);
    }

    [HttpPut("OpenPayPeriod")]
    [Permission(Policy.CanOpenPayPeriod)]
    [ProducesResponseType(typeof(OpenPayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OpenPayPeriod([FromBody] OpenPayPeriod.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<OpenPayPeriod.Response>(result);
    }
}