using Cleansia.Core.AppServices.Features.Addresses;
using Cleansia.Infra.Services.Geocoding;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

/// <summary>
/// Address autocomplete for the booking wizard, and the map thumbnail that confirms the
/// picked address.
///
/// Anonymous, because booking does not require an account. Rate limited, because every
/// call here is a billed third-party request made on behalf of someone who has not
/// identified themselves.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AddressSearchController(IMediator mediator, IGeocodingService geocodingService)
    : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [EnableRateLimiting("interactive")]
    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchAddresses.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string? country,
        [FromQuery] string? language,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new SearchAddresses.Query(q, country, language, limit <= 0 ? 5 : limit),
            cancellationToken);
        return HandleResult<SearchAddresses.Response>(result);
    }

    /// <summary>
    /// The map image for a picked coordinate.
    ///
    /// It answers with bytes rather than a DTO, so it takes the geocoding service
    /// directly instead of going through MediatR: a BusinessResult wrapper around a
    /// byte[] would be a pipeline stage that classifies nothing and a DTO nothing reads.
    /// A missing image is a 404 and not an error page — the panel is designed to render
    /// without one, which is also what an unprovisioned environment gets.
    /// </summary>
    [AllowAnonymous]
    [EnableRateLimiting("interactive")]
    [HttpGet("map")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Map(
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken cancellationToken)
    {
        var map = await geocodingService.GetStaticMapAsync(lat, lng, cancellationToken);
        if (map == null)
        {
            return NotFound();
        }

        // The same coordinate always renders the same tile, and a customer re-enters an
        // address across sessions. A day of caching keeps repeat views off a billed
        // endpoint. Public: the image is a map of an address the requester supplied, so
        // it carries nothing about who asked for it.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(map.Content, map.ContentType);
    }
}
