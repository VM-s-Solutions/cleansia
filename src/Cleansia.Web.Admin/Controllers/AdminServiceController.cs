using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminServiceController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewServices)]
    [ProducesResponseType(typeof(PagedData<ServiceListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedServices(
        [FromQuery] GetPagedServices.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// All service categories, sorted by DisplayOrder. Drives the category
    /// picker in the admin Service create/edit form. Same shape as the
    /// customer-facing categories that ship inside ServiceListItem.
    /// </summary>
    [HttpGet("categories")]
    [Permission(Policy.CanViewServices)]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetServiceCategories.Request(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{serviceId}")]
    [Permission(Policy.CanViewServices)]
    [ProducesResponseType(typeof(AdminServiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceById(
        string serviceId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetServiceById.Query(serviceId), cancellationToken);
        return HandleResult<AdminServiceDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreateService)]
    [ProducesResponseType(typeof(CreateService.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateService(
        [FromBody] CreateService.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateService.Response>(result);
    }

    [HttpPut("update/{serviceId}")]
    [Permission(Policy.CanUpdateService)]
    [ProducesResponseType(typeof(UpdateService.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateService(
        string serviceId,
        [FromBody] UpdateService.Command command,
        CancellationToken cancellationToken)
    {
        if (command.ServiceId != serviceId)
        {
            return BadRequest("Service ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateService.Response>(result);
    }

    [HttpPost("deactivate/{serviceId}")]
    [Permission(Policy.CanUpdateService)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeactivateService.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateService(
        string serviceId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateService.Command(serviceId), cancellationToken);
        return HandleResult<DeactivateService.Response>(result);
    }

    [HttpPost("activate/{serviceId}")]
    [Permission(Policy.CanUpdateService)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ActivateService.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateService(
        string serviceId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivateService.Command(serviceId), cancellationToken);
        return HandleResult<ActivateService.Response>(result);
    }

    [HttpDelete("delete/{serviceId}")]
    [Permission(Policy.CanDeleteService)]
    [ProducesResponseType(typeof(DeleteService.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteService(
        string serviceId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteService.Command(serviceId), cancellationToken);
        return HandleResult<DeleteService.Response>(result);
    }
}