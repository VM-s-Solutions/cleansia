using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.CashHeld;
using Cleansia.Core.AppServices.Features.CashHeld.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminCashHeldController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-all")]
    [Permission(Policy.CanViewCashHeld)]
    [ProducesResponseType(typeof(IReadOnlyList<CleanerCashHeldDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCashHeldByCleaners.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<CleanerCashHeldDto>>(result);
    }

    [HttpPost("record-remittance")]
    [Permission(Policy.CanRecordCashRemittance)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RecordCashRemittance.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RecordRemittance([FromBody] RecordCashRemittance.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RecordCashRemittance.Response>(result);
    }

    [HttpPost("write-off")]
    [Permission(Policy.CanWriteOffCashHeld)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(WriteOffCashHeld.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> WriteOff([FromBody] WriteOffCashHeld.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<WriteOffCashHeld.Response>(result);
    }
}
