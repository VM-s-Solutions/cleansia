using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MembershipController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpPost("Subscribe")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(CreateMembershipSubscription.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Subscribe(
        [FromBody] CreateMembershipSubscription.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateMembershipSubscription.Response>(result);
    }

    [HttpPost("Cancel")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(CancelMembershipSubscription.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CancelMembershipSubscription.Command(), cancellationToken);
        return HandleResult<CancelMembershipSubscription.Response>(result);
    }

    [HttpGet("GetMine")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(GetMyMembership.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyMembership.Query(), cancellationToken);
        return HandleResult<GetMyMembership.Response>(result);
    }

    [HttpPost("CreateCheckoutSession")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(CreateMembershipCheckoutSession.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateMembershipCheckoutSession.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateMembershipCheckoutSession.Response>(result);
    }

    [AllowAnonymous]
    [HttpGet("GetPlans")]
    [ProducesResponseType(typeof(IReadOnlyList<GetMembershipPlans.Response>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMembershipPlans.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<GetMembershipPlans.Response>>(result);
    }

    [HttpPost("SwapPlan")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(SwapMembershipPlan.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwapPlan(
        [FromBody] SwapMembershipPlan.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SwapMembershipPlan.Response>(result);
    }
}
