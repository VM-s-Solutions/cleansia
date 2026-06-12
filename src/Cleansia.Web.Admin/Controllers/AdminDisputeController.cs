using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminDisputeController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewDisputeListAdmin)]
    [ProducesResponseType(typeof(PagedData<DisputeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedDisputes([FromQuery] GetPagedDisputes.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{disputeId}")]
    [Permission(Policy.CanViewDisputeAdmin)]
    [ProducesResponseType(typeof(DisputeDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDisputeById(string disputeId, CancellationToken cancellationToken)
    {
        var query = new GetDisputeDetails.Query(disputeId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<DisputeDetails>(result);
    }

    [HttpPost("resolve")]
    [Permission(Policy.CanResolveDispute)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDispute([FromBody] ResolveDispute.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpPost("update-status")]
    [Permission(Policy.CanUpdateDisputeStatus)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateDisputeStatus.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }

    // ADR-0001 §D2 Note C, Q-0005: staff dispute replies are Admin-only. This is the
    // staff-reply AddMessage endpoint that moved off the Partner host. The staff flag is host-derived
    // (forced to true here), mirroring the JWT-enrichment `command with { ... }` idiom; the handler
    // then re-derives it from the caller's Administrator profile as the inner gate.
    [HttpPost("add-message")]
    [Permission(Policy.CanRespondToDispute)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMessage([FromBody] AddDisputeMessage.Command command, CancellationToken cancellationToken)
    {
        var enriched = command with { IsStaffMessage = true };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<object>(result);
    }
}
