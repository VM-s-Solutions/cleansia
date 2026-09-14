using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.Domain.Legal;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

/// <summary>
/// Read-only: every version of every legal text the platform has shown, with its texts. There is no
/// authoring here — a new version is a seed file under a new effective date and a deploy.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AdminLegalController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-versions")]
    [Permission(Policy.CanViewCountryConfigurations)]
    [ProducesResponseType(typeof(IReadOnlyList<LegalDocumentVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetVersions(
        [FromQuery] LegalDocumentAudience? audience,
        [FromQuery] LegalDocumentType? type,
        [FromQuery] string? countryId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AdminGetLegalVersions.Query(audience, type, countryId), cancellationToken);
        return HandleResult<IReadOnlyList<LegalDocumentVersionDto>>(result);
    }

    [HttpGet("get-document/{id}")]
    [Permission(Policy.CanViewCountryConfigurations)]
    [ProducesResponseType(typeof(AdminLegalDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDocument(
        string id,
        [FromQuery] string? language,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AdminGetLegalDocument.Query(id, language), cancellationToken);
        return HandleResult<AdminLegalDocumentDto>(result);
    }
}
