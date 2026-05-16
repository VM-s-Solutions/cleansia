using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Web.Mobile.Customer.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Mobile.Customer.Controllers;

/// <summary>
/// Customer Mobile API auth endpoints. Mirrors the Customer Web AuthController
/// in the commands it dispatches (<see cref="Login.Command"/> permissive for
/// customer users; <see cref="PartnerLogin.Command"/> would reject them), but
/// returns the body tokens directly without going through the HttpOnly cookie
/// / CSRF flow that the web hosts use — native clients (Android, iOS) can't
/// read HttpOnly cookies and need the token in the JSON response to store in
/// <c>EncryptedSharedPreferences</c> / Keychain.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AuthController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [AllowAnonymous]
    [HttpPost("Register")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] Register.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<bool>(result);
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] Login.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<JwtTokenResponse>(result);
    }

    [AllowAnonymous]
    [HttpPost("GoogleAuth")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuth.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<JwtTokenResponse>(result);
    }

    [AllowAnonymous]
    [HttpPut("ConfirmUserEmail")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmUserEmail([FromBody] ConfirmUserEmail.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<JwtTokenResponse>(result);
    }

    [AllowAnonymous]
    [HttpPost("ResendConfirmationEmail")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmail.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<bool>(result);
    }

    [AllowAnonymous]
    [HttpPost("ForgotPassword")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] RequestPasswordChange.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<bool>(result);
    }

    [AllowAnonymous]
    [HttpPost("RefreshToken")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshToken.Command command, CancellationToken cancellationToken)
    {
        // Pin the refresh check to the Customer audience — tokens issued by
        // this host should refresh only here. Mirrors how the partner Mobile
        // host pins to JwtAudiences.Mobile, and Customer Web pins to
        // JwtAudiences.Customer.
        var enriched = command with { RequiredAudience = JwtAudiences.Customer };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<JwtTokenResponse>(result);
    }

    [Authorize]
    [HttpPost("Logout")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] Logout.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<bool>(result);
    }
}
