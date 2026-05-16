using Cleansia.Config.Filters;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.EmployeePayroll.DTOs;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Partner.Abstractions;
using Cleansia.Web.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
[RequireCompleteProfile]
public class EmployeePayrollController(IMediator mediator) : ApiController(mediator)
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvoiceById(string invoiceId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetInvoiceById.Query(invoiceId), cancellationToken);
        return HandleResult<EmployeeInvoiceDetailDto>(result);
    }

    [HttpGet("GetPeriodPays")]
    [Permission(Policy.CanViewPeriodPays)]
    [ProducesResponseType(typeof(PeriodPaySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPeriodPays([FromQuery] GetPeriodPays.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<PeriodPaySummaryDto>(result);
    }

    [HttpPost("CalculateOrderPay")]
    [Permission(Policy.CanCalculateOrderPay)]
    [ProducesResponseType(typeof(CalculateOrderPay.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CalculateOrderPay([FromBody] CalculateOrderPay.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CalculateOrderPay.Response>(result);
    }

    [HttpPost("GenerateInvoice")]
    [Permission(Policy.CanGenerateInvoice)]
    [ProducesResponseType(typeof(GenerateInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GenerateInvoice([FromBody] GenerateInvoice.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<GenerateInvoice.Response>(result);
    }

    [HttpPut("ApproveInvoice")]
    [Permission(Policy.CanApproveInvoice)]
    [ProducesResponseType(typeof(ApproveInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveInvoice([FromBody] ApproveInvoice.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<ApproveInvoice.Response>(result);
    }

    [HttpPut("MarkInvoicePaid")]
    [Permission(Policy.CanMarkInvoicePaid)]
    [ProducesResponseType( typeof(MarkInvoicePaid.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkInvoicePaid([FromBody] MarkInvoicePaid.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<MarkInvoicePaid.Response>(result);
    }

    [HttpPut("CancelInvoice")]
    [Permission(Policy.CanCancelInvoice)]
    [ProducesResponseType(typeof(CancelInvoice.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelInvoice([FromBody] CancelInvoice.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CancelInvoice.Response>(result);
    }

    [HttpPut("ClosePayPeriod")]
    [Permission(Policy.CanClosePayPeriod)]
    [ProducesResponseType(typeof(ClosePayPeriod.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ClosePayPeriod([FromBody] ClosePayPeriod.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<ClosePayPeriod.Response>(result);
    }

    [HttpPost("RegenerateInvoicePdf")]
    [Permission(Policy.CanGenerateInvoice)]
    [ProducesResponseType(typeof(RegenerateInvoicePdf.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegenerateInvoicePdf([FromBody] RegenerateInvoicePdf.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<RegenerateInvoicePdf.Response>(result);
    }

    [HttpGet("DownloadInvoice/{invoiceId}")]
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
