using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.InvoiceTemplates;
using Cleansia.Core.AppServices.Features.InvoiceTemplates.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminInvoiceTemplateController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("get-paged")]
    [Permission(Policy.CanViewInvoiceTemplates)]
    [ProducesResponseType(typeof(PagedData<InvoiceTemplateListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedInvoiceTemplates(
        [FromBody] GetPagedInvoiceTemplates.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{invoiceTemplateId}")]
    [Permission(Policy.CanViewInvoiceTemplates)]
    [ProducesResponseType(typeof(InvoiceTemplateDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceTemplateById(
        string invoiceTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetInvoiceTemplateById.Query(invoiceTemplateId), cancellationToken);
        return HandleResult<InvoiceTemplateDetailDto>(result);
    }

    [HttpPost]
    [Permission(Policy.CanCreateInvoiceTemplate)]
    [ProducesResponseType(typeof(CreateInvoiceTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInvoiceTemplate(
        [FromBody] CreateInvoiceTemplate.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateInvoiceTemplate.Response>(result);
    }

    [HttpPut("{invoiceTemplateId}")]
    [Permission(Policy.CanUpdateInvoiceTemplate)]
    [ProducesResponseType(typeof(UpdateInvoiceTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateInvoiceTemplate(
        string invoiceTemplateId,
        [FromBody] UpdateInvoiceTemplate.Command command,
        CancellationToken cancellationToken)
    {
        if (command.InvoiceTemplateId != invoiceTemplateId)
        {
            return BadRequest("Invoice Template ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateInvoiceTemplate.Response>(result);
    }

    [HttpDelete("{invoiceTemplateId}")]
    [Permission(Policy.CanDeleteInvoiceTemplate)]
    [ProducesResponseType(typeof(DeleteInvoiceTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteInvoiceTemplate(
        string invoiceTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteInvoiceTemplate.Command(invoiceTemplateId), cancellationToken);
        return HandleResult<DeleteInvoiceTemplate.Response>(result);
    }

    [HttpPost("{invoiceTemplateId}/activate")]
    [Permission(Policy.CanActivateInvoiceTemplate)]
    [ProducesResponseType(typeof(ActivateInvoiceTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateInvoiceTemplate(
        string invoiceTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivateInvoiceTemplate.Command(invoiceTemplateId), cancellationToken);
        return HandleResult<ActivateInvoiceTemplate.Response>(result);
    }

    [HttpPost("{invoiceTemplateId}/deactivate")]
    [Permission(Policy.CanActivateInvoiceTemplate)]
    [ProducesResponseType(typeof(DeactivateInvoiceTemplate.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateInvoiceTemplate(
        string invoiceTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateInvoiceTemplate.Command(invoiceTemplateId), cancellationToken);
        return HandleResult<DeactivateInvoiceTemplate.Response>(result);
    }

    [HttpGet("{invoiceTemplateId}/download")]
    [Permission(Policy.CanViewInvoiceTemplates)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInvoiceTemplate(
        string invoiceTemplateId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DownloadInvoiceTemplate.Query(invoiceTemplateId), cancellationToken);

        if (!result.IsSuccess || result.Value == null)
        {
            return NotFound();
        }

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}