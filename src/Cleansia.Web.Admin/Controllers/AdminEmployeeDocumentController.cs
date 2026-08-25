using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminEmployeeDocumentController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("get-paged")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(PagedData<EmployeeDocumentItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<EmployeeDocumentItem>> GetPagedDocuments([FromBody] GetEmployeeDocuments.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpPost("{documentId}/approve")]
    [Permission(Policy.CanApproveEmployeeDocument)]
    [ProducesResponseType(typeof(ApproveDocument.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveDocument(string documentId, [FromBody] ApproveDocument.Command? request, CancellationToken cancellationToken)
    {
        var command = new ApproveDocument.Command
        {
            DocumentId = documentId,
            Notes = request?.Notes
        };
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ApproveDocument.Response>(result);
    }

    [HttpPost("{documentId}/reject")]
    [Permission(Policy.CanRejectEmployeeDocument)]
    [ProducesResponseType(typeof(RejectDocument.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectDocument(string documentId, [FromBody] RejectDocument.Command? request, CancellationToken cancellationToken)
    {
        var command = new RejectDocument.Command
        {
            DocumentId = documentId,
            Notes = request?.Notes
        };
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RejectDocument.Response>(result);
    }
    
    [HttpGet("{documentId}/versions")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(GetDocumentVersionHistory.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVersionHistory(string documentId, CancellationToken cancellationToken)
    {
        var query = new GetDocumentVersionHistory.Query { DocumentId = documentId };
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<GetDocumentVersionHistory.Response>(result);
    }

    [HttpGet("{documentId}/download")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadDocument(string documentId, CancellationToken cancellationToken)
    {
        var query = new DownloadEmployeeDocument.Query(documentId);
        var result = await Mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult<DownloadEmployeeDocument.Response>(result);
        }

        return File(
            result.Value.FileBytes,
            result.Value.ContentType,
            result.Value.FileName);
    }

    /// <summary>
    /// The document types this country expects. These rows are what
    /// <c>ApproveEmployee</c> gates on, which is why they are admin-managed rather than a constant:
    /// requirements change with the law, and a change that needs a release is a change that waits for one.
    /// </summary>
    [HttpGet("requirements/{countryId}")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(IEnumerable<DocumentRequirementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<DocumentRequirementDto>> GetRequirements(
        string countryId,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetDocumentRequirements.Request(countryId), cancellationToken);
    }

    /// <summary>
    /// An upsert, not an insert. (CountryId, DocumentType) is unique, so a second row for the same pair
    /// would not be a variant of the rule — it would be two rules disagreeing, with whichever the query
    /// read first winning.
    /// </summary>
    [HttpPut("requirements")]
    [Permission(Policy.CanAdminUpdateEmployee)]
    [ProducesResponseType(typeof(SaveDocumentRequirement.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveRequirement(
        [FromBody] SaveDocumentRequirement.Request request,
        CancellationToken cancellationToken)
    {
        var command = new SaveDocumentRequirement.Command(
            request.CountryId, request.DocumentType, request.IsRequired, request.SortOrder);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SaveDocumentRequirement.Response>(result);
    }

    /// <summary>
    /// Removing a row un-gates the type. It does not un-approve anybody: approval is decided at the
    /// moment an admin approves, and the requirements are an input to that decision rather than a
    /// standing property of the cleaner.
    /// </summary>
    [HttpDelete("requirements/{requirementId}")]
    [Permission(Policy.CanAdminUpdateEmployee)]
    [ProducesResponseType(typeof(DeleteDocumentRequirement.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteRequirement(string requirementId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteDocumentRequirement.Command(requirementId), cancellationToken);
        return HandleResult<DeleteDocumentRequirement.Response>(result);
    }

    /// <summary>
    /// The deletion queue. Defaults to Pending because the queue is a to-do list: an admin opening it
    /// wants what is still waiting on them, not a history. Answered requests are reachable by asking for
    /// a status, so the record is not hidden — it is just not what the screen opens on.
    /// </summary>
    [HttpGet("deletion-requests")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(IEnumerable<DocumentDeletionRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<DocumentDeletionRequestDto>> GetDeletionRequests(
        [FromQuery] DocumentDeletionRequestStatus? status,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetDocumentDeletionRequests.Request(status), cancellationToken);
    }

    /// <summary>
    /// Answer a cleaner's request. Approving here is the ONLY thing that removes the document — the
    /// request itself never touched it, which is why one left unanswered costs the cleaner nothing.
    /// </summary>
    [HttpPost("deletion-requests/{requestId}/resolve")]
    [Permission(Policy.CanApproveEmployeeDocument)]
    [ProducesResponseType(typeof(ResolveDocumentDeletionRequest.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDeletionRequest(
        string requestId,
        [FromBody] ResolveDocumentDeletionRequest.Request request,
        CancellationToken cancellationToken)
    {
        var command = new ResolveDocumentDeletionRequest.Command(requestId, request.Approve, request.Notes);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ResolveDocumentDeletionRequest.Response>(result);
    }
}
