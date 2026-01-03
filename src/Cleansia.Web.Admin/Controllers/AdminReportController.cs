using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Reports;
using Cleansia.Core.AppServices.Features.Reports.DTOs;
using Cleansia.Core.AppServices.Features.Reports.Filters;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminReportController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("revenue")]
    [Permission(Policy.CanViewRevenueReport)]
    [ProducesResponseType(typeof(RevenueReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRevenueReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var filter = new ReportFilter(startDate, endDate);
        var result = await Mediator.Send(new GetRevenueReport.Query(filter), cancellationToken);
        return HandleResult<RevenueReportDto>(result);
    }

    [HttpGet("payroll")]
    [Permission(Policy.CanViewPayrollReport)]
    [ProducesResponseType(typeof(PayrollReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPayrollReport([FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var filter = new ReportFilter(startDate, endDate);
        var result = await Mediator.Send(new GetPayrollReport.Query(filter), cancellationToken);
        return HandleResult<PayrollReportDto>(result);
    }
}