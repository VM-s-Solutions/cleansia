using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Core.AppServices.Features.PropertySizes;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CountryController(IMediator mediator) : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<CountryListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CountryListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCountryOverview.Request(), cancellationToken);
    }

    /// <summary>
    /// Countries the company actually operates in. Customer pickers MUST use
    /// this — GetOverview is only for legacy/admin paths.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("GetServiced")]
    [ProducesResponseType(typeof(IEnumerable<CountryListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CountryListItem>> GetServiced(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServicedCountries.Request(), cancellationToken);
    }

    /// <summary>
    /// The property sizes a market offers, labelled for the caller's language.
    /// </summary>
    /// <remarks>
    /// Anonymous because the home-page price calculator is the caller and it runs
    /// before anyone signs in. Read-only catalogue data with no per-visitor
    /// content, so there is nothing here to scope.
    ///
    /// An unknown or unseeded country returns an empty list rather than a 404 —
    /// a market we hold no presets for is one we do not serve yet.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("GetPropertySizes")]
    [ProducesResponseType(typeof(IEnumerable<GetPropertySizePresets.PropertySizePresetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IReadOnlyList<GetPropertySizePresets.PropertySizePresetDto>> GetPropertySizes(
        [FromQuery] string isoCode,
        [FromQuery] string languageCode,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(
            new GetPropertySizePresets.Request(isoCode, languageCode), cancellationToken);
    }
}
