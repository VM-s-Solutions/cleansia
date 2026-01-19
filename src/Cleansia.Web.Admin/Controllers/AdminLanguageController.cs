using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Languages;
using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminLanguageController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-overview")]
    [Permission(Policy.CanViewLanguages)]
    [ProducesResponseType(typeof(IEnumerable<LanguageListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<LanguageListItem>> GetLanguages(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetLanguageOverview.Request(), cancellationToken);
    }

    [HttpGet("details/{languageId}")]
    [Permission(Policy.CanViewLanguages)]
    [ProducesResponseType(typeof(LanguageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLanguageById(
        string languageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetLanguageById.Query(languageId), cancellationToken);
        return HandleResult<LanguageDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreateLanguage)]
    [ProducesResponseType(typeof(CreateLanguage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateLanguage(
        [FromBody] CreateLanguage.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateLanguage.Response>(result);
    }

    [HttpPut("update/{languageId}")]
    [Permission(Policy.CanUpdateLanguage)]
    [ProducesResponseType(typeof(UpdateLanguage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLanguage(
        string languageId,
        [FromBody] UpdateLanguage.Command command,
        CancellationToken cancellationToken)
    {
        if (command.LanguageId != languageId)
        {
            return BadRequest("Language ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateLanguage.Response>(result);
    }

    [HttpDelete("delete/{languageId}")]
    [Permission(Policy.CanDeleteLanguage)]
    [ProducesResponseType(typeof(DeleteLanguage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLanguage(
        string languageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteLanguage.Command(languageId), cancellationToken);
        return HandleResult<DeleteLanguage.Response>(result);
    }
}