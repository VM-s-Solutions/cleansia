using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Features.Refunds.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminRefundController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewOrderDetailAdmin)]
    [ProducesResponseType(typeof(PagedData<RefundListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedRefunds([FromQuery] GetPagedRefunds.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("retry")]
    [Permission(Policy.CanIssueRefund)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RetryRefund.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RetryRefund([FromBody] RetryRefund.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RetryRefund.Response>(result);
    }

    [HttpPost("partial")]
    [Permission(Policy.CanIssueRefund)]
    // S5 / ADR-0003: narrow brute-force window on this money-out mutation. Reuses the REGISTERED "auth"
    // policy (10/min, partitioned per JWT sub / client IP) — defense-in-depth over the deterministic
    // RefundKey idempotency collapse, matching the other side-effecting admin mutations.
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(IssuePartialRefund.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IssuePartialRefund(
        [FromBody] IssuePartialRefund.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<IssuePartialRefund.Response>(result);
    }
}
