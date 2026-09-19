using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.Domain.Legal;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

/// <summary>
/// The legal texts a customer reads and accepts — the terms and the privacy policy in force for a
/// market, rendered. Anonymous: the register form and the booking wizard link here before anyone
/// signs in, and the version on the page is the one a consent row stamps.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class LegalController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [AllowAnonymous]
    [EnableRateLimiting("interactive")]
    [HttpGet("GetDocument")]
    [ProducesResponseType(typeof(LegalDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDocument(
        [FromQuery] LegalDocumentType type,
        [FromQuery] string? countryId,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetLegalDocument.Query(type, countryId, language), cancellationToken);
        return HandleResult<LegalDocumentDto>(result);
    }
}
