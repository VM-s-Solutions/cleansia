using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminPayConfigController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPayConfigs)]
    [ProducesResponseType(typeof(PagedData<EmployeePayConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedPayConfigs(
        [FromQuery] GetPagedPayConfigs.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{payConfigId}")]
    [Permission(Policy.CanViewPayConfig)]
    [ProducesResponseType(typeof(EmployeePayConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayConfigById(
        string payConfigId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPayConfigById.Query(payConfigId), cancellationToken);
        return HandleResult<EmployeePayConfigDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreatePayConfig)]
    [ProducesResponseType(typeof(CreatePayConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePayConfig(
        [FromBody] CreatePayConfig.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreatePayConfig.Response>(result);
    }

    [HttpPut("update/{payConfigId}")]
    [Permission(Policy.CanUpdatePayConfig)]
    [ProducesResponseType(typeof(UpdatePayConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePayConfig(
        string payConfigId,
        [FromBody] UpdatePayConfig.Command command,
        CancellationToken cancellationToken)
    {
        if (command.PayConfigId != payConfigId)
        {
            return BadRequest("Pay config ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdatePayConfig.Response>(result);
    }

    [HttpDelete("delete/{payConfigId}")]
    [Permission(Policy.CanDeletePayConfig)]
    [ProducesResponseType(typeof(DeletePayConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayConfig(
        string payConfigId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeletePayConfig.Command(payConfigId), cancellationToken);
        return HandleResult<DeletePayConfig.Response>(result);
    }

    [HttpGet("employee-summary/{employeeId}")]
    [Permission(Policy.CanViewPayConfigs)]
    [ProducesResponseType(typeof(EmployeePayConfigSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEmployeePayConfigSummary(
        string employeeId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetEmployeePayConfigSummary.Query(employeeId), cancellationToken);
        return HandleResult<EmployeePayConfigSummaryDto>(result);
    }

    [HttpPost("bulk-create-for-employee")]
    [Permission(Policy.CanCreatePayConfig)]
    [ProducesResponseType(typeof(BulkCreateEmployeePayConfigs.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkCreateForEmployee(
        [FromBody] BulkCreateEmployeePayConfigs.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<BulkCreateEmployeePayConfigs.Response>(result);
    }
}
