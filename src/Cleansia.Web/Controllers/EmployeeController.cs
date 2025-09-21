using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
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
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(RegistrationCompletionStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentEmployee([FromQuery] CheckCurrentEmployee.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        return HandleResult<RegistrationCompletionStatus>(result);
    }
}