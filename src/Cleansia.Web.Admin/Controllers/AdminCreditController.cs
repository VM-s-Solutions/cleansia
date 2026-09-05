using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Credit.Admin;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminCreditController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("issue")]
    [Permission(Policy.CanIssueCustomerCredit)]
    // S5 / ADR-0003: this is money out. Reuses the REGISTERED "auth" policy (10/min, partitioned per
    // JWT sub / client IP) — the tightest registered window, matching partial-refund and manual
    // loyalty grants. Defense-in-depth over the RequestId idempotency collapse: the unique index makes
    // a retry harmless, this makes a retry STORM harmless too.
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(IssueCustomerCredit.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Issue(
        [FromBody] IssueCustomerCredit.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<IssueCustomerCredit.Response>(result);
    }

    [HttpGet("user/{userId}")]
    [Permission(Policy.CanViewUserCredit)]
    [ProducesResponseType(typeof(GetUserCredit.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserCredit(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new GetUserCredit.Query(userId), cancellationToken);
        return HandleResult<GetUserCredit.Response>(result);
    }
}
