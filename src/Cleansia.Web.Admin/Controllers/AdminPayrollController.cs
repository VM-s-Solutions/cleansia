using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminPayrollController(IMediator mediator) : ApiController(mediator)
{
    [HttpPut("update-invoice-amounts")]
    [Permission(Policy.CanUpdateInvoiceAmounts)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UpdateInvoiceAmounts.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateInvoiceAmounts([FromBody] UpdateInvoiceAmounts.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateInvoiceAmounts.Response>(result);
    }

    [HttpPut("dispute-invoice")]
    [Permission(Policy.CanDisputeInvoice)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DisputeInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DisputeInvoice([FromBody] DisputeInvoice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<DisputeInvoice.Response>(result);
    }

    [HttpPut("reject-invoice")]
    [Permission(Policy.CanRejectInvoice)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RejectInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectInvoice([FromBody] RejectInvoice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RejectInvoice.Response>(result);
    }

    [HttpPost("generate-invoice")]
    [Permission(Policy.CanGenerateInvoice)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(GenerateInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateInvoice([FromBody] GenerateInvoice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<GenerateInvoice.Response>(result);
    }
}
