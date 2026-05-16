using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PromoCodeController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpPost("Validate")]
    [Permission(Policy.CanRedeemPromoCode)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ValidatePromoCode.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidatePromoCode.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ValidatePromoCode.Response>(result);
    }
}
