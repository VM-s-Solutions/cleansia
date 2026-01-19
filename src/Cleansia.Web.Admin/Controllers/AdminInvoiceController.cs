using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminInvoiceController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPagedInvoices)]
    [ProducesResponseType(typeof(PagedData<EmployeeInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedInvoices([FromQuery] GetPagedInvoices.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{invoiceId}")]
    [Permission(Policy.CanViewPagedInvoices)]
    [ProducesResponseType(typeof(EmployeeInvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvoiceDetails(string invoiceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetInvoiceById.Query(invoiceId), cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpPut("approve")]
    [Permission(Policy.CanApproveInvoice)]
    [ProducesResponseType(typeof(ApproveInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveInvoice([FromBody] ApproveInvoice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ApproveInvoice.Response>(result);
    }

    [HttpPut("mark-paid")]
    [Permission(Policy.CanMarkInvoicePaid)]
    [ProducesResponseType(typeof(MarkInvoicePaid.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkInvoicePaid([FromBody] MarkInvoicePaid.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<MarkInvoicePaid.Response>(result);
    }

    [HttpPut("cancel")]
    [Permission(Policy.CanCancelInvoice)]
    [ProducesResponseType(typeof(CancelInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelInvoice([FromBody] CancelInvoice.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CancelInvoice.Response>(result);
    }

    [HttpPost("regenerate-pdf")]
    [Permission(Policy.CanGenerateInvoice)]
    [ProducesResponseType(typeof(RegenerateInvoicePdf.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegenerateInvoicePdf([FromBody] RegenerateInvoicePdf.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RegenerateInvoicePdf.Response>(result);
    }

    [HttpGet("download/{invoiceId}")]
    [Permission(Policy.CanViewPagedInvoices)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadInvoice(string invoiceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DownloadInvoice.Query(invoiceId), cancellationToken);

        if (result == null)
        {
            return NotFound("Invoice or PDF not found");
        }

        return File(result.PdfBytes, "application/pdf", result.FileName);
    }
}
