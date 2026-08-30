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
    /// Issues a first-order discount code to an address that has no account yet
    /// and queues the e-mail carrying it.
    /// </summary>
    /// <remarks>
    /// Anonymous by necessity — the caller has no account, that is the point, so
    /// it carries a bare [AllowAnonymous] and no [Permission], which is how every
    /// other anonymous route on these hosts is written (ADR-0001 §D1.2 — the
    /// allow-list is frozen at seven and a new anonymous route does not extend it).
    /// Rate-limited on the "auth" policy (10/min per client IP for an anonymous
    /// caller) because it can cause an e-mail to be sent to an arbitrary address.
    /// The code is derived from the address, so the queue key is stable and the
    /// consumer's idempotency claim lets exactly one promo e-mail reach an
    /// address ever, however many times this is called.
    ///
    /// Always 200 on a well-formed address, and the body never carries the code:
    /// answering differently for a known address would turn this into an account
    /// probe, and returning the code would hand a discount to anyone who can
    /// guess an e-mail. → /architecture/security-rules
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
