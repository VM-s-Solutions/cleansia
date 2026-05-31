using Cleansia.Core.AppServices.Features.ServiceAreas;
using Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServiceCityController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceCityDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<ServiceCityDto>> GetServiceCities(
        [FromQuery] string? countryId,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServiceCities.Request(countryId), cancellationToken);
    }
}
