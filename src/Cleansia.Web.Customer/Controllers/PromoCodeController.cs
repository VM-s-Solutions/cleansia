using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PromoCodeController(IMediator mediator) : CustomerApiController(mediator)
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

    /// <summary>
    /// Queues a first-order discount code once per address; repeats return an already-sent error.
    /// </summary>
    /// <remarks>
    /// → /flows/loyalty-and-memberships#public-promo-code-requests
    /// </remarks>
    [HttpPost("Request")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RequestPromoCode.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestPromoCode(
        [FromBody] RequestPromoCode.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RequestPromoCode.Response>(result);
    }
}
