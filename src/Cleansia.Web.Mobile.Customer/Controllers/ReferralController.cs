using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReferralController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpGet("GetMy")]
    [Permission(Policy.CanViewMyReferral)]
    [ProducesResponseType(typeof(GetMyReferral.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMy(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyReferral.Query(), cancellationToken);
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
        return await Mediator.Send(new GetMyReferrals.Request { Offset = offset, Limit = limit }, cancellationToken);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("Validate")]
    [ProducesResponseType(typeof(ValidateReferral.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateReferral.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<ValidateReferral.Response>(result);
    }
}
