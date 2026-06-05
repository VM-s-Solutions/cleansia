using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Web.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController(IMediator mediator) : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("CreateOrder")]
    [ProducesResponseType(typeof(CreateOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrder.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CreateOrder.Response>(result);
    }

    /// <summary>
    /// Mobile PaymentSheet flow: convert an existing card-payable order into
    /// a Stripe PaymentIntent + ephemeral key. Authenticated only — guest
    /// checkout uses the web Checkout Session flow.
    /// </summary>
    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("CreatePaymentIntent")]
    [ProducesResponseType(typeof(CreatePaymentIntent.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntent.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CreatePaymentIntent.Response>(result);
    }

    // SEC-W3 (T-0116) — per-source-IP webhook window (independent of "auth"/"interactive").
    // [AllowAnonymous] preserved: Stripe is unauthenticated; the signature check is the real auth,
    // the rate limit is the unauthenticated-DoS cap on this side-effecting endpoint (S5).
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
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
