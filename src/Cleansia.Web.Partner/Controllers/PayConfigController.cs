using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Partner.Abstractions;
using Cleansia.Web.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PayConfigController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetPagedPayConfigs")]
    [Permission(Policy.CanViewPayConfigs)]
    [ProducesResponseType(typeof(PagedData<EmployeePayConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<EmployeePayConfigDto>> GetPagedPayConfigs([FromQuery] GetPagedPayConfigs.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetPayConfigById")]
    [Permission(Policy.CanViewPayConfig)]
    [ProducesResponseType(typeof(EmployeePayConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPayConfigById([FromQuery] GetPayConfigById.Query query)
    {
        var result = await Mediator.Send(query);
        return HandleResult<EmployeePayConfigDto>(result);
    }

    [HttpPost("CreatePayConfig")]
    [Permission(Policy.CanCreatePayConfig)]
    [ProducesResponseType(typeof(CreatePayConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePayConfig([FromBody] CreatePayConfig.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CreatePayConfig.Response>(result);
    }

    [HttpPut("UpdatePayConfig")]
    [Permission(Policy.CanUpdatePayConfig)]
    [ProducesResponseType(typeof(UpdatePayConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePayConfig([FromBody] UpdatePayConfig.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<UpdatePayConfig.Response>(result);
    }

    [HttpDelete("DeletePayConfig")]
    [Permission(Policy.CanDeletePayConfig)]
    [ProducesResponseType(typeof(DeletePayConfig.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePayConfig([FromBody] DeletePayConfig.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<DeletePayConfig.Response>(result);
    }
}
