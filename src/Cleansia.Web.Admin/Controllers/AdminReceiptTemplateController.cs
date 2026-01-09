using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.ReceiptTemplates;
using Cleansia.Core.AppServices.Features.ReceiptTemplates.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminReceiptTemplateController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("get-paged")]
    [Permission(Policy.CanViewReceiptTemplates)]
    [ProducesResponseType(typeof(PagedData<ReceiptTemplateListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedReceiptTemplates(
        [FromBody] GetPagedReceiptTemplates.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{receiptTemplateId}")]
    [Permission(Policy.CanViewReceiptTemplates)]
    [ProducesResponseType(typeof(ReceiptTemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceiptTemplateById(
        string receiptTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetReceiptTemplateById.Query(receiptTemplateId), cancellationToken);
        return HandleResult<ReceiptTemplateDetailDto>(result);
    }

    [HttpPost]
    [Permission(Policy.CanCreateReceiptTemplate)]
    [ProducesResponseType(typeof(CreateReceiptTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateReceiptTemplate(
        [FromBody] CreateReceiptTemplate.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateReceiptTemplate.Response>(result);
    }

    [HttpPut("{receiptTemplateId}")]
    [Permission(Policy.CanUpdateReceiptTemplate)]
    [ProducesResponseType(typeof(UpdateReceiptTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReceiptTemplate(
        string receiptTemplateId,
        [FromBody] UpdateReceiptTemplate.Command command,
        CancellationToken cancellationToken)
    {
        if (command.ReceiptTemplateId != receiptTemplateId)
        {
            return BadRequest("Receipt Template ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateReceiptTemplate.Response>(result);
    }

    [HttpDelete("{receiptTemplateId}")]
    [Permission(Policy.CanDeleteReceiptTemplate)]
    [ProducesResponseType(typeof(DeleteReceiptTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReceiptTemplate(
        string receiptTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteReceiptTemplate.Command(receiptTemplateId), cancellationToken);
        return HandleResult<DeleteReceiptTemplate.Response>(result);
    }

    [HttpPost("{receiptTemplateId}/activate")]
    [Permission(Policy.CanActivateReceiptTemplate)]
    [ProducesResponseType(typeof(ActivateReceiptTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateReceiptTemplate(
        string receiptTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivateReceiptTemplate.Command(receiptTemplateId), cancellationToken);
        return HandleResult<ActivateReceiptTemplate.Response>(result);
    }

    [HttpPost("{receiptTemplateId}/deactivate")]
    [Permission(Policy.CanActivateReceiptTemplate)]
    [ProducesResponseType(typeof(DeactivateReceiptTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateReceiptTemplate(
        string receiptTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateReceiptTemplate.Command(receiptTemplateId), cancellationToken);
        return HandleResult<DeactivateReceiptTemplate.Response>(result);
    }

    [HttpGet("{receiptTemplateId}/download")]
    [Permission(Policy.CanViewReceiptTemplates)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReceiptTemplate(
        string receiptTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DownloadReceiptTemplate.Query(receiptTemplateId), cancellationToken);

        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}