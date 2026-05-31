using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.ServiceAreas;
using Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminServiceCityController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet]
    [Permission(Policy.CanViewServiceCities)]
    [ProducesResponseType(typeof(IEnumerable<ServiceCityDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<ServiceCityDto>> GetServiceCities(
        [FromQuery] string? countryId,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServiceCities.Request(countryId), cancellationToken);
    }

    [HttpPost]
    [Permission(Policy.CanManageServiceCities)]
    [ProducesResponseType(typeof(CreateServiceCity.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateServiceCity(
        [FromBody] CreateServiceCity.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateServiceCity.Response>(result);
    }

    [HttpPut("{id}")]
    [Permission(Policy.CanManageServiceCities)]
    [ProducesResponseType(typeof(UpdateServiceCity.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateServiceCity(
        string id,
        [FromBody] UpdateServiceCity.Command command,
        CancellationToken cancellationToken)
    {
        if (command.Id != id)
        {
            return BadRequest("Id in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateServiceCity.Response>(result);
    }

    [HttpDelete("{id}")]
    [Permission(Policy.CanManageServiceCities)]
    [ProducesResponseType(typeof(DeleteServiceCity.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteServiceCity(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteServiceCity.Command(id), cancellationToken);
        return HandleResult<DeleteServiceCity.Response>(result);
    }
}
