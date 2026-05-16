using Cleansia.Core.AppServices.Features.Languages;
using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Web.Mobile.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LanguageController(IMediator mediator) : MobileApiController(mediator)
{
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<LanguageListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<LanguageListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetLanguageOverview.Request(), cancellationToken);
    }
}
