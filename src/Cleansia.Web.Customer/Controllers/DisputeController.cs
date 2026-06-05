using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DisputeController(IMediator mediator) : CustomerApiController(mediator)
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

    [HttpPost("AddMessage")]
    [Permission(Policy.CanAddDisputeMessage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMessage([FromBody] AddDisputeMessage.Command command, CancellationToken cancellationToken)
    {
        // The staff flag is host-derived, not body-supplied. A customer never authors a
        // staff message — force IsStaffMessage=false (mirrors the JWT-enrichment `command with` idiom).
        var enriched = command with { IsStaffMessage = false };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpPost("UploadEvidence")]
    [Permission(Policy.CanUploadDisputeEvidence)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDisputeEvidence.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadEvidence(
        [FromForm] string disputeId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "File is required." });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);

        var command = new UploadDisputeEvidence.Command(
            DisputeId: disputeId,
            FileName: file.FileName,
            ContentType: file.ContentType,
            FileData: ms.ToArray()
        );

        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UploadDisputeEvidence.Response>(result);
    }
}
