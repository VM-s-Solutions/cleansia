using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.AppServices.Features.Referrals.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminReferralController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewReferrals)]
    [ProducesResponseType(typeof(PagedData<AdminReferralListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedReferrals(
        [FromQuery] ReferralStatus? status,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetPagedReferrals.Query(status, dateFrom, dateTo, offset, limit),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-user/{userId}")]
    [Permission(Policy.CanViewReferrals)]
    [ProducesResponseType(typeof(GetReferralsByUser.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReferralsByUser(
        string userId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetReferralsByUser.Query(userId), cancellationToken);
        return HandleResult<GetReferralsByUser.Response>(result);
    }
}
