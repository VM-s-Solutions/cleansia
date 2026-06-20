using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DisputeController(IMediator mediator) : CustomerDisputeControllerBase(mediator)
{
    [HttpPost("Create")]
    [Permission(Policy.CanCreateDispute)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> CreateDispute([FromBody] CreateDispute.Command command, CancellationToken cancellationToken)
        => CreateDisputeCore(command, cancellationToken);

    [HttpGet("GetById/{disputeId}")]
    [Permission(Policy.CanViewDispute)]
    [ProducesResponseType(typeof(DisputeDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetDisputeById(string disputeId, CancellationToken cancellationToken)
        => GetDisputeByIdCore(disputeId, cancellationToken);

    [HttpGet("GetPaged")]
    [Permission(Policy.CanViewDisputeList)]
    [ProducesResponseType(typeof(PagedData<DisputeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<PagedData<DisputeListItem>> GetPagedDisputes([FromQuery] GetPagedDisputes.Request request, CancellationToken cancellationToken)
        => GetPagedDisputesCore(request, cancellationToken);

    [HttpPost("AddMessage")]
    [Permission(Policy.CanAddDisputeMessage)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> AddMessage([FromBody] AddDisputeMessage.Command command, CancellationToken cancellationToken)
        => AddMessageCore(command, cancellationToken);

    [HttpPost("UploadEvidence")]
    [Permission(Policy.CanUploadDisputeEvidence)]
    [EnableRateLimiting("auth")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadDisputeEvidence.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> UploadEvidence([FromForm] string disputeId, IFormFile file, CancellationToken cancellationToken)
        => UploadEvidenceCore(disputeId, file, cancellationToken);
}
