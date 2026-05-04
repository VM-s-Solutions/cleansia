using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReferralController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("GetMy")]
    [Permission(Policy.CanViewMyReferral)]
    [ProducesResponseType(typeof(GetMyReferral.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMy(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new GetMyReferral.Query(userId), cancellationToken);
        return HandleResult<GetMyReferral.Response>(result);
    }

    [HttpGet("GetMyReferrals")]
    [Permission(Policy.CanViewMyReferral)]
    [ProducesResponseType(typeof(PagedData<GetMyReferrals.ReferralListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<GetMyReferrals.ReferralListItem>> GetMyReferrals(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return await Mediator.Send(new GetMyReferrals.Query(userId, offset, limit), cancellationToken);
    }

    /// <summary>
    /// Pre-submit validation for a referral code. Anonymous-friendly so the
    /// signup form can call it before the user has a token; the controller
    /// fills in <c>AcceptingUserId</c> from the JWT when present so the
    /// service can apply self-referral / already-referred checks.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("Validate")]
    [ProducesResponseType(typeof(ValidateReferral.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateReferral.Command command, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { AcceptingUserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<ValidateReferral.Response>(result);
    }
}
