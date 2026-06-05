using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminDisputeController(IMediator mediator) : ApiController(mediator)
{
    // SEC-DSP-01 (ADR-0001 §D2 Note C, Q-0005): staff dispute replies are Admin-only. This is the
    // staff-reply AddMessage endpoint that moved off the Partner host. The staff flag is host-derived
    // (forced to true here), mirroring the JWT-enrichment `command with { ... }` idiom; the handler
    // then re-derives it from the caller's Administrator profile as the inner gate.
    [HttpPost("AddMessage")]
    [Permission(Policy.CanRespondToDispute)]
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
