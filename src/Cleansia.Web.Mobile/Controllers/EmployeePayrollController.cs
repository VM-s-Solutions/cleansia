using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Mobile.Abstractions;
using Cleansia.Web.Mobile.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeePayrollController(IMediator mediator) : MobileApiController(mediator)
{
    [HttpGet("GetPagedInvoices")]
    [Permission(Policy.CanViewPagedInvoices)]
    [ProducesResponseType(typeof(PagedData<EmployeeInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<EmployeeInvoiceDto>> GetPagedInvoices([FromQuery] GetPagedInvoices.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetInvoiceById/{invoiceId}")]
    [Permission(Policy.CanViewPagedInvoices)]
    [ProducesResponseType(typeof(EmployeeInvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvoiceById(string invoiceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetInvoiceById.Query(invoiceId), cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet("DownloadInvoice/{invoiceId}")]
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
