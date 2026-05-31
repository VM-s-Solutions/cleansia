using Cleansia.Core.AppServices.Features.ServiceAreas;
using Cleansia.Core.AppServices.Features.ServiceAreas.DTOs;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ServiceCityController(IMediator mediator) : CustomerApiController(mediator)
{
    /// <summary>
    /// Cities the company actually serves. Customer order wizard MUST
    /// validate that the picked address's city matches one of these (in
    /// addition to backend re-validation).
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceCityDto>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<ServiceCityDto>> GetServiceCities(
        [FromQuery] string? countryId,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServiceCities.Request(countryId), cancellationToken);
    }
}
