using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminEmployeeController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("get-paged")]
    [Permission(Policy.CanViewPagedEmployee)]
    [ProducesResponseType(typeof(PagedData<AdminEmployeeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedEmployees([FromBody] GetPagedEmployees.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{employeeId}/approve")]
    [Permission(Policy.CanApproveEmployee)]
    [ProducesResponseType(typeof(ApproveEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveEmployee(string employeeId, [FromBody] ApproveEmployee.Request? request, CancellationToken cancellationToken)
    {
        var command = new ApproveEmployee.Command(employeeId, request?.Notes);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ApproveEmployee.Response>(result);
    }

    [HttpPost("{employeeId}/reject")]
    [Permission(Policy.CanRejectEmployee)]
    [ProducesResponseType(typeof(RejectEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RejectEmployee(string employeeId, [FromBody] RejectEmployee.Request? request, CancellationToken cancellationToken)
    {
        var command = new RejectEmployee.Command(employeeId, request?.Reason);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RejectEmployee.Response>(result);
    }

    [HttpGet("details/{employeeId}")]
    [Permission(Policy.CanViewPagedEmployee)]
    [ProducesResponseType(typeof(AdminEmployeeDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEmployeeDetail(string employeeId, CancellationToken cancellationToken)
    {
        var query = new GetEmployeeDetail.Query(employeeId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<AdminEmployeeDetail>(result);
    }
}
