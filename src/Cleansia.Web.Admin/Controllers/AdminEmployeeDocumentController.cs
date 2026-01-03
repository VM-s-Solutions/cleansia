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
}
