using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.SavedCards;
using Cleansia.Core.AppServices.Features.SavedCards.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SavedCardController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpPost("CreateSetupIntent")]
    [Permission(Policy.CanManageSavedCard)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(CreateSavedCardSetupIntent.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSetupIntent(
        [FromBody] CreateSavedCardSetupIntent.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateSavedCardSetupIntent.Response>(result);
    }

    [HttpGet("GetMine")]
    [Permission(Policy.CanManageSavedCard)]
    [ProducesResponseType(typeof(IReadOnlyList<SavedCardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMySavedCards.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<SavedCardDto>>(result);
    }

    [HttpDelete("Remove/{id}")]
    [Permission(Policy.CanManageSavedCard)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RemoveSavedCard.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Remove(string id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RemoveSavedCard.Command(id), cancellationToken);
        return HandleResult<RemoveSavedCard.Response>(result);
    }
}
