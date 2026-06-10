using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminMembershipController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewMembershipPlans)]
    [ProducesResponseType(typeof(PagedData<MembershipPlanListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedMembershipPlans(
        [FromQuery] bool? active,
        [FromQuery] string? search,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetPagedMembershipPlans.Query(active, search, offset, limit),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{membershipPlanId}")]
    [Permission(Policy.CanViewMembershipPlans)]
    [ProducesResponseType(typeof(MembershipPlanDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembershipPlanById(
        string membershipPlanId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMembershipPlanById.Query(membershipPlanId), cancellationToken);
        return HandleResult<MembershipPlanDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreateMembershipPlan)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(CreateMembershipPlan.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateMembershipPlan(
        [FromBody] CreateMembershipPlan.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateMembershipPlan.Response>(result);
    }

    [HttpPut("update/{membershipPlanId}")]
    [Permission(Policy.CanUpdateMembershipPlan)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UpdateMembershipPlan.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMembershipPlan(
        string membershipPlanId,
        [FromBody] UpdateMembershipPlan.Command command,
        CancellationToken cancellationToken)
    {
        if (command.MembershipPlanId != membershipPlanId)
        {
            return BadRequest("Membership plan ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateMembershipPlan.Response>(result);
    }

    [HttpPost("deactivate/{membershipPlanId}")]
    [Permission(Policy.CanDeactivateMembershipPlan)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeactivateMembershipPlan.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateMembershipPlan(
        string membershipPlanId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateMembershipPlan.Command(membershipPlanId), cancellationToken);
        return HandleResult<DeactivateMembershipPlan.Response>(result);
    }
}
