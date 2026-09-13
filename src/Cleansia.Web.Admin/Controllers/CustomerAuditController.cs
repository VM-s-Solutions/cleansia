using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CustomerAuditController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewAuditLog)]
    [ProducesResponseType(typeof(PagedData<CustomerActionAuditDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedCustomerActionAudits([FromQuery] GetPagedCustomerActionAudits.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("get-by-id/{auditId}")]
    [Permission(Policy.CanViewAuditLog)]
    [ProducesResponseType(typeof(CustomerActionAuditDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCustomerActionAuditById(string auditId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCustomerActionAuditById.Query(auditId), cancellationToken);
        return HandleResult<CustomerActionAuditDetailDto>(result);
    }

    [HttpGet("timeline")]
    [Permission(Policy.CanViewAuditLog)]
    [ProducesResponseType(typeof(PagedData<TimelineEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActionTimeline([FromQuery] GetActionTimeline.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return HandleResult<PagedData<TimelineEntryDto>>(result);
    }
}
