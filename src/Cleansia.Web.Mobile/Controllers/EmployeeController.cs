using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Web.Mobile.Abstractions;
using Cleansia.Web.Mobile.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmployeeController(IMediator mediator) : MobileApiController(mediator)
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
    [ProducesResponseType(typeof(UpdateEmployee.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEmployee([FromBody] UpdateEmployee.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<UpdateEmployee.Response>(result);
    }

    [HttpPut("UpdatePersonalInfo")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(UpdatePersonalInfo.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePersonalInfo([FromBody] UpdatePersonalInfo.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdatePersonalInfo.Response>(result);
    }

    [HttpPut("UpdateIdentificationInfo")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(UpdateIdentificationInfo.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateIdentificationInfo([FromBody] UpdateIdentificationInfo.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdateIdentificationInfo.Response>(result);
    }

    [HttpPut("UpdateAddressInfo")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(UpdateAddressInfo.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAddressInfo([FromBody] UpdateAddressInfo.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdateAddressInfo.Response>(result);
    }

    [HttpPut("UpdateBankDetails")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(UpdateBankDetails.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateBankDetails([FromBody] UpdateBankDetails.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdateBankDetails.Response>(result);
    }

    [HttpPut("UpdateEmergencyContact")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(UpdateEmergencyContact.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEmergencyContact([FromBody] UpdateEmergencyContact.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdateEmergencyContact.Response>(result);
    }

    [HttpPut("UpdateAvailability")]
    [Permission(Policy.CanUpdateCurrentEmployee)]
    [ProducesResponseType(typeof(UpdateAvailability.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAvailability([FromBody] UpdateAvailability.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdateAvailability.Response>(result);
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
