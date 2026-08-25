using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Features.EmployeeDocuments.DTOs;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Partner.Abstractions;
using Cleansia.Web.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeeController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("CheckCurrentEmployee")]
    [Permission(Policy.CanCheckCurrentEmployee)]
    [ProducesResponseType(typeof(RegistrationCompletionStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckCurrentEmployee([FromQuery] CheckCurrentEmployee.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<RegistrationCompletionStatus>(result);
    }

    [HttpGet("GetCurrentEmployee")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(EmployeeItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentEmployee([FromQuery] GetCurrentEmployeeDetail.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<EmployeeItem>(result);
    }

    [HttpPut("UpdateEmployee")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UpdateEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEmployee([FromBody] UpdateEmployee.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<UpdateEmployee.Response>(result);
    }

    [HttpPut("UpdateBankDetails")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UpdateBankDetails.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateBankDetails([FromBody] UpdateBankDetails.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateBankDetails.Response>(result);
    }

    [HttpPut("UpdateJobRadius")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UpdateJobRadius.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateJobRadius([FromBody] UpdateJobRadius.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdateJobRadius.Response>(result);
    }

    [HttpGet("GetMyPayoutDetails")]
    [Permission(Policy.CanViewEmployeePayoutDetails)]
    [ProducesResponseType(typeof(MyPayoutDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPayoutDetails([FromQuery] GetMyPayoutDetails.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<MyPayoutDetails>(result);
    }

    [HttpPost("SaveMyDocuments")]
    [Permission(Policy.CanUploadEmployeeDocument)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(SaveMyDocuments.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveMyDocuments([FromBody] SaveMyDocuments.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SaveMyDocuments.Response>(result);
    }

    [HttpGet("GetMyDocuments")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(GetMyDocuments.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyDocuments([FromQuery] GetMyDocuments.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<GetMyDocuments.Response>(result);
    }

    /// <summary>
    /// Ask an admin to remove a document. It removes NOTHING — the document stays active until the
    /// request is answered.
    ///
    /// <para>This replaced the partner-facing delete. That button removed the document immediately,
    /// and the soft-delete flipped AreDocumentsUploaded, which re-engaged the registration lock: one
    /// tap cost a cleaner their access to work. Deleting is now admin-only, because some of these
    /// documents are proof the employer is required to hold.</para>
    ///
    /// <para>To swap a document for a newer one, use ReplaceMyDocument instead — that needs no
    /// permission, because the slot never empties.</para>
    /// </summary>
    [HttpPost("RequestMyDocumentDeletion/{documentId}")]
    [Permission(Policy.CanDeleteEmployeeDocument)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RequestMyDocumentDeletion.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestMyDocumentDeletion(
        string documentId,
        [FromBody] RequestMyDocumentDeletion.Request request,
        CancellationToken cancellationToken)
    {
        var command = new RequestMyDocumentDeletion.Command(documentId, request.Reason);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RequestMyDocumentDeletion.Response>(result);
    }

    /// <summary>
    /// Supersede one of the cleaner's own documents with a newer file. No admin needed: the new
    /// version is created before the old one is retired, so the document count never dips and the
    /// registration lock never re-engages.
    /// </summary>
    [HttpPut("ReplaceMyDocument/{documentId}")]
    [Permission(Policy.CanUploadEmployeeDocument)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ReplaceMyDocument.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReplaceMyDocument(
        string documentId,
        [FromBody] ReplaceMyDocument.Request request,
        CancellationToken cancellationToken)
    {
        var command = new ReplaceMyDocument.Command(documentId, request.File, request.Description);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ReplaceMyDocument.Response>(result);
    }

    [HttpGet("DownloadMyDocument")]
    [Permission(Policy.CanDownloadEmployeeDocument)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadMyDocument([FromQuery] DownloadMyDocument.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult<DownloadMyDocument.Response>(result);
        }

        return File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
    }

    /// <summary>
    /// What this cleaner still owes, resolved against what they have already uploaded.
    ///
    /// <para>The documents screen used to open on an empty box that named nothing, so the first step of
    /// onboarding was contacting support to ask which papers we wanted. This answers that in the app.</para>
    /// </summary>
    [HttpGet("GetMyDocumentRequirements")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(IEnumerable<MyDocumentRequirementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IEnumerable<MyDocumentRequirementDto>> GetMyDocumentRequirements(
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetMyDocumentRequirements.Request(), cancellationToken);
    }
}
