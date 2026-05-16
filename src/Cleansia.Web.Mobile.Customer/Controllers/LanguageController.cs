using Cleansia.Core.AppServices.Features.Languages;
using Cleansia.Core.AppServices.Features.Languages.DTOs;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LanguageController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpGet("GetOverview")]
    [ProducesResponseType(typeof(IEnumerable<LanguageListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<LanguageListItem>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetLanguageOverview.Request(), cancellationToken);
    }
}
