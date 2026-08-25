using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Web.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CountryController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<CountryListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CountryListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCountryOverview.Request(), cancellationToken);
    }

    [HttpGet("GetServiced")]
    [ProducesResponseType(typeof(IEnumerable<CountryListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<CountryListItem>> GetServiced(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetServicedCountries.Request(), cancellationToken);
    }

    /// <summary>
    /// What this country calls its business identifiers, and whether it demands them.
    ///
    /// <para>CountryConfiguration has carried these since it was seeded and no endpoint returned them,
    /// so every client hardcoded the Czech "IČO" in its own translation files — which a Polish or
    /// Ukrainian partner then read. The label belongs to the country, not to the app's language.</para>
    /// </summary>
    [HttpGet("GetFieldLabels/{countryId}")]
    [ProducesResponseType(typeof(GetCountryFieldLabels.CountryFieldLabelsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFieldLabels(string countryId, CancellationToken cancellationToken)
    {
        var labels = await Mediator.Send(new GetCountryFieldLabels.Request(countryId), cancellationToken);
        return labels is null ? NotFound() : Ok(labels);
    }
}