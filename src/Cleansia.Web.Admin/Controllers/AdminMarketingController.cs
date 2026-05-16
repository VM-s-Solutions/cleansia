using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Marketing;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminMarketingController(IMediator mediator) : ApiController(mediator)
{
    /// <summary>
    /// Enqueue a sitewide push campaign. The actual per-user fan-out runs
    /// asynchronously in <c>SendSitewidePromoFanoutFunction</c> — this
    /// endpoint returns as soon as the single fan-out message is queued.
    /// </summary>
    [HttpPost("send-sitewide-promo")]
    [Permission(Policy.CanSendSitewidePromo)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendSitewidePromo(
        [FromBody] SendSitewidePromo.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }
}
