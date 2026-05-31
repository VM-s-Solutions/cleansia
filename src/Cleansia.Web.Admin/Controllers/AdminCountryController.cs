using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.AppServices.Features.Countries.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminCountryController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-overview")]
    [Permission(Policy.CanViewCountries)]
    [ProducesResponseType(typeof(IEnumerable<CountryListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<CountryListItem>> GetCountries(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCountryOverview.Request(), cancellationToken);
    }

    [HttpGet("details/{countryId}")]
    [Permission(Policy.CanViewCountries)]
    [ProducesResponseType(typeof(CountryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCountryById(
        string countryId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCountryById.Query(countryId), cancellationToken);
        return HandleResult<CountryDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreateCountry)]
    [ProducesResponseType(typeof(CreateCountry.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCountry(
        [FromBody] CreateCountry.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateCountry.Response>(result);
    }

    [HttpPut("update/{countryId}")]
    [Permission(Policy.CanUpdateCountry)]
    [ProducesResponseType(typeof(UpdateCountry.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCountry(
        string countryId,
        [FromBody] UpdateCountry.Command command,
        CancellationToken cancellationToken)
    {
        if (command.CountryId != countryId)
        {
            return BadRequest("Country ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateCountry.Response>(result);
    }

    [HttpDelete("delete/{countryId}")]
    [Permission(Policy.CanDeleteCountry)]
    [ProducesResponseType(typeof(DeleteCountry.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCountry(
        string countryId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteCountry.Command(countryId), cancellationToken);
        return HandleResult<DeleteCountry.Response>(result);
    }

    /// <summary>
    /// Toggle whether the company operates in this country. Drives every
    /// customer/partner-facing country picker via the GetServiced endpoint.
    /// </summary>
    [HttpPut("{countryId}/serviced")]
    [Permission(Policy.CanUpdateCountry)]
    [ProducesResponseType(typeof(SetCountryServiced.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetServiced(
        string countryId,
        [FromBody] SetCountryServicedRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new SetCountryServiced.Command(countryId, request.IsServiced), cancellationToken);
        return HandleResult<SetCountryServiced.Response>(result);
    }

    public record SetCountryServicedRequest(bool IsServiced);
}