using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Abstractions;
using Cleansia.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeeController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("CheckCurrentEmployee")]
    [Permission(Policy.CanCheckCurrentEmployee)]
    [ProducesResponseType(typeof(RegistrationCompletionStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckCurrentEmployee([FromQuery] CheckCurrentEmployee.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<RegistrationCompletionStatus>(result);
    }

    [HttpGet("GetCurrentEmployee")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(EmployeeItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentEmployee([FromQuery] GetCurrentEmployeeDetail.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<EmployeeItem>(result);
    }

    [HttpPut("UpdateEmployee")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(CreateOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEmployee([FromBody] UpdateEmployee.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<UpdateEmployee.Response>(result);
    }

    [HttpGet]
    [Permission(Policy.CanViewPagedEmployee)]
    [ProducesResponseType(typeof(PagedData<AdminEmployeeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<AdminEmployeeListItem>> GetPagedEmployees([FromQuery] GetPagedEmployees.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpPost("{employeeId}/approve")]
    [Permission(Policy.CanApproveEmployee)]
    [ProducesResponseType(typeof(ApproveEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApproveEmployee(string employeeId, CancellationToken cancellationToken)
    {
        var command = new ApproveEmployee.Command(employeeId);
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

    [HttpPost("SaveMyDocuments")]
    [Permission(Policy.CanUploadEmployeeDocument)]
    [ProducesResponseType(typeof(SaveMyDocuments.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SaveMyDocuments([FromBody] SaveMyDocuments.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SaveMyDocuments.Response>(result);
    }

    [HttpGet("GetMyDocuments")]
    [Permission(Policy.CanViewEmployeeDocuments)]
    [ProducesResponseType(typeof(GetMyDocuments.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyDocuments([FromQuery] GetMyDocuments.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<GetMyDocuments.Response>(result);
    }

    [HttpDelete("DeleteMyDocument")]
    [Permission(Policy.CanDeleteEmployeeDocument)]
    [ProducesResponseType(typeof(DeleteMyDocument.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteMyDocument([FromQuery] DeleteMyDocument.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<DeleteMyDocument.Response>(result);
    }

    [HttpGet("DownloadMyDocument")]
    [Permission(Policy.CanDownloadEmployeeDocument)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadMyDocument([FromQuery] DownloadMyDocument.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult<DownloadMyDocument.Response>(result);
        }

        return File(result.Value.FileBytes, result.Value.ContentType, result.Value.FileName);
    }
}
