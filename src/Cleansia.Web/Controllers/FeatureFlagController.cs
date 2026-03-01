using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.FeatureFlags;
using Cleansia.Web.Abstractions;
using Cleansia.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FeatureFlagController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("check")]
    [Permission(Policy.CanCheckFeatureFlag)]
    [ProducesResponseType(typeof(CheckFeatureFlag.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> Check([FromQuery] string featureName, [FromQuery] string? countryId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CheckFeatureFlag.Query(featureName, countryId), cancellationToken);
        return HandleResult<CheckFeatureFlag.Response>(result);
    }
}
