using Cleansia.Config.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AuthController(
    IMediator mediator,
    AuthCookieWriter cookieWriter,
    AuthCookieConfig cookieConfig) : CookieAuthApiController(mediator, cookieWriter, cookieConfig)
{
    [AllowAnonymous]
    [HttpPost("Register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] Register.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<object>(result);
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] Login.Command command)
    {
        var enriched = command with { TrustedDeviceToken = RefreshTokenFromCookieOrBody(string.Empty) };
        var result = await Mediator.Send(enriched);

        return HandleTokenIssuingResult(result);
    }

    [AllowAnonymous]
    [HttpPost("GoogleAuth")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuth.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleTokenIssuingResult(result);
    }

    // The browser flow's counterpart to the mobile host's AppleAuth. Same command and handler; the only
    // difference is that this host answers with HttpOnly cookies + a CSRF token rather than a bare body
    // token, so it must go through HandleTokenIssuingResult. The audience the identity token is checked
    // against is this host's own (Apple:WebServicesId, never the native bundle id) — see appsettings.json.
    // Deliberately NOT mirrored onto the partner/admin hosts: User.Profile defaults to Customer and
    // CreateWithApple never sets it, so exposing this there would let anyone self-provision customer rows.
    [AllowAnonymous]
    [HttpPost("AppleAuth")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AppleAuth([FromBody] AppleAuth.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleTokenIssuingResult(result);
    }

    [AllowAnonymous]
    [HttpPut("ConfirmUserEmail")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmUserEmail([FromBody] ConfirmUserEmail.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleTokenIssuingResult(result);
    }

    [AllowAnonymous]
    [HttpPost("ResendConfirmationEmail")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmail.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<object>(result);
    }

    [AllowAnonymous]
    [HttpPost("RefreshToken")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshToken.Command command, CancellationToken cancellationToken)
    {
        var enriched = command with
        {
            Token = RefreshTokenFromCookieOrBody(command.Token),
            RequiredProfile = UserProfile.Customer,
            RequiredAudience = JwtAudiences.Customer,
        };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleTokenIssuingResult(result);
    }

    [Authorize]
    [HttpPost("Logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] Logout.Command command, CancellationToken cancellationToken)
    {
        var enriched = command with { Token = RefreshTokenFromCookieOrBody(command.Token) };
        var result = await Mediator.Send(enriched, cancellationToken);
        ClearAuthCookies();
        return HandleResult<object>(result);
    }
}
