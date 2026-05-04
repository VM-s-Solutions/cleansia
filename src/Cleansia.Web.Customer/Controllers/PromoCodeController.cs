using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PromoCodeController(IMediator mediator) : CustomerApiController(mediator)
{
    /// <summary>
    /// Validate a promo code + return the computed discount. Used by the
    /// booking wizard for instant feedback as the customer types. Does NOT
    /// redeem the code — actual redemption happens inside CreateOrder.
    /// </summary>
    [HttpPost("Validate")]
    [Permission(Policy.CanRedeemPromoCode)]
    [ProducesResponseType(typeof(ValidatePromoCode.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidatePromoCode.Command command,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var enriched = command with { UserId = userId };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<ValidatePromoCode.Response>(result);
    }
}
