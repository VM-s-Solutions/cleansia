using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
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
}