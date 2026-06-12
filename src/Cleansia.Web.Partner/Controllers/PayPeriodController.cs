using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PayPeriods;
using Cleansia.Core.AppServices.Features.PayPeriods.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Partner.Abstractions;
using Cleansia.Web.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

/// <summary>
/// Partner-host pay-period surface — READ-ONLY by design. The full mutation surface
/// (Create/Update/Delete/Open/Close/MarkPaid/Reopen) lives ONLY on the Admin host's
/// <c>AdminPayPeriodController</c>, per the per-audience-host seam: pay periods are an admin/payroll
/// concern, so the Partner API exposes only the two reads a partner-facing screen needs. The previously
/// duplicated mutation endpoints (AdminOnly-gated, so never cleaner-exploitable, but redundant write
/// surface) were removed; authz holds regardless of host.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class PayPeriodController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetPagedPayPeriods")]
    [Permission(Policy.CanViewPayPeriods)]
    [ProducesResponseType(typeof(PagedData<PayPeriodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<PayPeriodDto>> GetPagedPayPeriods([FromQuery] GetPagedPayPeriods.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetPayPeriodById")]
    [Permission(Policy.CanViewPayPeriod)]
    [ProducesResponseType(typeof(PayPeriodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPayPeriodById([FromQuery] GetPayPeriodById.Query query)
    {
        var result = await Mediator.Send(query);
        return HandleResult<PayPeriodDto>(result);
    }
}