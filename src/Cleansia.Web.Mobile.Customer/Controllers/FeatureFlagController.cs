using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.FeatureFlags;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FeatureFlagController(IMediator mediator) : CustomerMobileApiController(mediator)
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
