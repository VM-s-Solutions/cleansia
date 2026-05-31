using Cleansia.Core.AppServices.Features.ServiceAreas;
using Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;
using Cleansia.Web.Mobile.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Partner.Controllers;

/// <summary>
/// Mirrors Cleansia.Web.Mobile.Customer.ServiceCityController so the
/// partner mobile app can render service-area info on the address
/// section ("we service jobs in your city" / "you can still take jobs
/// in nearby serviced cities"). Cleaner home addresses are not blocked
/// by city; only customer order creation is. The endpoint exists here
/// just to feed the informational UI on the partner side.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class ServiceCityController(IMediator mediator) : MobileApiController(mediator)
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
