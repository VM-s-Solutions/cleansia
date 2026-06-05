using Cleansia.Config.Filters;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Partner.Abstractions;
using Cleansia.Web.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
[RequireCompleteProfile]
public class DisputeController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("Create")]
    [Permission(Policy.CanCreateDispute)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDispute([FromBody] CreateDispute.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<string>(result);
    }

    [HttpGet("GetById/{disputeId}")]
    [Permission(Policy.CanViewDispute)]
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

    [HttpGet("GetPaged")]
    [Permission(Policy.CanViewDisputeList)]
    [ProducesResponseType(typeof(PagedData<DisputeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<DisputeListItem>> GetPagedDisputes([FromQuery] GetPagedDisputes.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    // SEC-DSP-01 (ADR-0001 §D2 Note C, Q-0005): the staff-reply AddMessage endpoint moved OFF the
    // Partner host to the Admin host (AdminDisputeController, gated CanRespondToDispute=AdminOnly).
    // No cleaner posts a dispute message of any kind on Partner. The customer self-reply lives on the
    // Customer/Mobile.Customer hosts under CanAddDisputeMessage.

    [HttpPost("Resolve")]
    [Permission(Policy.CanResolveDispute)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDispute([FromBody] ResolveDispute.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpPost("UpdateStatus")]
    [Permission(Policy.CanUpdateDisputeStatus)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateDisputeStatus.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }
}
