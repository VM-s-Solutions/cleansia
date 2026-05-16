using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Web.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController(IMediator mediator) : ApiController(mediator)
{
    [AllowAnonymous]
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();

        var command = new HandlePaymentNotification.Command(
            JsonPayload: json,
            SignatureHeader: signatureHeader);

        var result = await Mediator.Send(command);
        return HandleResult<string>(result);
    }
}
