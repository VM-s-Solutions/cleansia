using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

/// <summary>
/// Cleansia Plus subscription endpoints. All scoped to the authenticated
/// customer (no admin/employee access). UserId is enriched server-side from
/// the JWT NameIdentifier claim — clients never set it.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class MembershipController(IMediator mediator) : CustomerApiController(mediator)
{
    /// <summary>
    /// Two-phase subscribe flow. First call (PaymentMethodConfirmed=false)
    /// returns a SetupIntent client_secret + ephemeral key for the client to
    /// attach a payment method via Stripe SDK. Second call
    /// (PaymentMethodConfirmed=true) creates the Stripe subscription + local
    /// UserMembership row.
    /// </summary>
    [HttpPost("Subscribe")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(CreateMembershipSubscription.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Subscribe(
        [FromBody] CreateMembershipSubscription.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<CreateMembershipSubscription.Response>(result);
    }

    /// <summary>
    /// Cancel the user's active membership at the end of the current period.
    /// Benefits continue until <see cref="CancelMembershipSubscription.Response.EffectiveEndDate"/>.
    /// </summary>
    [HttpPost("Cancel")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(CancelMembershipSubscription.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new CancelMembershipSubscription.Command(UserId: userId), cancellationToken);
        return HandleResult<CancelMembershipSubscription.Response>(result);
    }

    /// <summary>
    /// "What's my Plus status?" — drives the management screen and any
    /// gating ("show this perk only for active Plus members"). Returns a
    /// HasMembership=false response when the user has no active subscription.
    /// </summary>
    [HttpGet("GetMine")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(GetMyMembership.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await Mediator.Send(new GetMyMembership.Query(userId), cancellationToken);
        return HandleResult<GetMyMembership.Response>(result);
    }

    /// <summary>
    /// Web subscribe path. Creates a Stripe-hosted Checkout Session in
    /// subscription mode and returns its URL. Caller redirects the browser
    /// to that URL; the local UserMembership row is provisioned by the
    /// <c>customer.subscription.created</c> webhook on success.
    /// </summary>
    [HttpPost("CreateCheckoutSession")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(CreateMembershipCheckoutSession.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCheckoutSession(
        [FromBody] CreateMembershipCheckoutSession.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<CreateMembershipCheckoutSession.Response>(result);
    }

    /// <summary>
    /// Plan catalog — drives the monthly/yearly switcher on the subscribe
    /// page. Anonymous-friendly so the marketing surface can render prices
    /// before the user signs in.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("GetPlans")]
    [ProducesResponseType(typeof(IReadOnlyList<GetMembershipPlans.Response>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMembershipPlans.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<GetMembershipPlans.Response>>(result);
    }

    /// <summary>
    /// Swap to a different plan (typically monthly → yearly upgrade).
    /// Stripe prorates the cost and charges/credits the customer's default
    /// payment method on the spot. Returns the new period end so the
    /// management UI can update without a follow-up GetMine round-trip.
    /// </summary>
    [HttpPost("SwapPlan")]
    [Permission(Policy.CanManageMembership)]
    [ProducesResponseType(typeof(SwapMembershipPlan.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SwapPlan(
        [FromBody] SwapMembershipPlan.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<SwapMembershipPlan.Response>(result);
    }
}
