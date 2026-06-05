using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminLoyaltyController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("grant-points")]
    [Permission(Policy.CanGrantLoyaltyPoints)]
    // T-0112 (LG-SEC-06 / S5 / ADR-0003): narrow brute-force window on this money-side-effecting
    // mutation. Reuses the REGISTERED "auth" policy (10/min, partitioned per JWT sub / client IP —
    // CleansiaStartupBase.AddRateLimiter), the tightest registered window, matching the other
    // side-effecting mutations (password-change, referral validate). Defense-in-depth on top of the
    // requestId idempotency collapse: throttles a retry storm at the edge.
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(GrantPointsManually.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GrantPoints(
        [FromBody] GrantPointsManually.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<GrantPointsManually.Response>(result);
    }

    [HttpPost("revoke-points")]
    [Permission(Policy.CanGrantLoyaltyPoints)]
    // T-0112 (LG-SEC-06 / S5 / ADR-0003): narrow brute-force window — REGISTERED "auth" policy
    // (10/min, partitioned). See grant-points above.
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RevokePointsManually.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokePoints(
        [FromBody] RevokePointsManually.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RevokePointsManually.Response>(result);
    }

    [HttpGet("user-account/{userId}")]
    [Permission(Policy.CanViewUserLoyalty)]
    [ProducesResponseType(typeof(GetUserLoyaltyAccount.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserLoyaltyAccount(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetUserLoyaltyAccount.Query(userId), cancellationToken);
        return HandleResult<GetUserLoyaltyAccount.Response>(result);
    }

    [HttpGet("user-activity/{userId}")]
    [Permission(Policy.CanViewUserLoyalty)]
    [ProducesResponseType(typeof(PagedData<GetUserLoyaltyActivity.ActivityItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserLoyaltyActivity(
        string userId,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetUserLoyaltyActivity.Query(userId, offset, limit),
            cancellationToken);
        return Ok(result);
    }
}
