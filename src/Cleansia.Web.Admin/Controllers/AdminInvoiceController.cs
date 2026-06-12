using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvoiceDetails(string invoiceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetInvoiceById.Query(invoiceId), cancellationToken);
        return HandleResult<EmployeeInvoiceDetailDto>(result);
    }

    [HttpPut("approve")]
    [Permission(Policy.CanApproveInvoice)]
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadInvoice(string invoiceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DownloadInvoice.Query(invoiceId), cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult<DownloadInvoice.Response>(result);
        }

        return File(result.Value!.PdfBytes, "application/pdf", result.Value.FileName);
    }
}
