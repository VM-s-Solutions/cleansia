using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminEmployeeController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPagedEmployee)]
    [ProducesResponseType(typeof(PagedData<AdminEmployeeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedEmployees([FromQuery] GetPagedEmployees.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{employeeId}/approve")]
    [Permission(Policy.CanApproveEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApproveEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveEmployee(string employeeId, [FromBody] ApproveEmployee.Request request, CancellationToken cancellationToken)
    {
        var command = new ApproveEmployee.Command(employeeId, request.WorkCountryId, request.Notes);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ApproveEmployee.Response>(result);
    }

    [HttpPost("{employeeId}/reject")]
    [Permission(Policy.CanRejectEmployee)]
    [EnableRateLimiting("auth")]
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

    [HttpGet("{employeeId}/payout-details")]
    [Permission(Policy.CanViewEmployeePayoutDetails)]
    [ProducesResponseType(typeof(MaskedPayoutDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEmployeePayoutDetails(string employeeId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEmployeePayoutDetails.Query(employeeId), cancellationToken);
        return HandleResult<MaskedPayoutDetails>(result);
    }

    // Masking only bounds exposure if the number of reveals does: an audited but unbounded reveal
    // records bulk exfiltration instead of stopping it, since the same policy that lists employee ids
    // grants this route.
    [HttpPost("{employeeId}/payout-details/reveal")]
    [Permission(Policy.CanRevealEmployeePayoutDetails)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RevealedPayoutDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevealEmployeePayoutDetails(string employeeId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RevealEmployeePayoutDetails.Command(employeeId), cancellationToken);
        return HandleResult<RevealedPayoutDetails>(result);
    }

    [HttpPut("{employeeId}/update-availability")]
    [Permission(Policy.CanAdminUpdateEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminUpdateEmployeeAvailability.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateEmployeeAvailability(string employeeId, [FromBody] AdminUpdateEmployeeAvailability.Request request, CancellationToken cancellationToken)
    {
        var command = new AdminUpdateEmployeeAvailability.Command(employeeId, request.Availability);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AdminUpdateEmployeeAvailability.Response>(result);
    }

    /// <summary>
    /// Caps or un-caps one cleaner's weekly order count. A null limit clears it back to unlimited,
    /// which is the default every cleaner has.
    /// </summary>
    [HttpPut("{employeeId}/weekly-order-limit")]
    [Permission(Policy.CanAdminUpdateEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminSetEmployeeWeeklyOrderLimit.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetWeeklyOrderLimit(
        string employeeId,
        [FromBody] AdminSetEmployeeWeeklyOrderLimit.Request request,
        CancellationToken cancellationToken)
    {
        var command = new AdminSetEmployeeWeeklyOrderLimit.Command(employeeId, request.WeeklyOrderLimit);
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AdminSetEmployeeWeeklyOrderLimit.Response>(result);
    }

    [HttpPut("{employeeId}/update")]
    [Permission(Policy.CanAdminUpdateEmployee)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminUpdateEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateEmployee(string employeeId, [FromBody] AdminUpdateEmployee.Command command, CancellationToken cancellationToken)
    {
        var updatedCommand = command with { EmployeeId = employeeId };
        var result = await Mediator.Send(updatedCommand, cancellationToken);
        return HandleResult<AdminUpdateEmployee.Response>(result);
    }
}
