using Cleansia.Core.AppServices.Features.FeatureFlags;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FeatureFlagController(IMediator mediator) : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [EnableRateLimiting("interactive")]
    [HttpGet("check")]
    [ProducesResponseType(typeof(CheckFeatureFlag.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> Check([FromQuery] string featureName, [FromQuery] string? countryId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CheckFeatureFlag.Query(featureName, countryId), cancellationToken);
        return HandleResult<CheckFeatureFlag.Response>(result);
    }
}
