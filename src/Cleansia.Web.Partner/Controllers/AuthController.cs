using Cleansia.Config.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Web.Partner.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AuthController(
    IMediator mediator,
    AuthCookieWriter cookieWriter,
    AuthCookieConfig cookieConfig) : ApiController(mediator)
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
    [HttpPost("RegisterEmployee")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterEmployee([FromBody] RegisterEmployee.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<bool>(result);
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] PartnerLogin.Command command)
    {
        var result = await Mediator.Send(command);

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
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmail.Command command, CancellationToken cancellationToken)
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
        // Refresh token lives in the HttpOnly cookie now — body is back-compat only.
        var token = cookieWriter.ReadRefreshTokenFromCookie(HttpContext, cookieConfig) ?? command.Token;
        var enriched = command with { Token = token, RequiredAudience = JwtAudiences.Partner };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleTokenIssuingResult(result);
    }

    [Authorize]
    [HttpPost("Logout")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] Logout.Command command, CancellationToken cancellationToken)
    {
        var token = cookieWriter.ReadRefreshTokenFromCookie(HttpContext, cookieConfig) ?? command.Token;
        var enriched = command with { Token = token };
        var result = await Mediator.Send(enriched, cancellationToken);
        // Always clear the cookies on logout, even when the server-side
        // revoke failed — the user pressed sign-out, they expect to be out.
        cookieWriter.ClearCookies(HttpContext, cookieConfig);
        return HandleResult<bool>(result);
    }

    // Augment successful token-issuing results with the HttpOnly cookies +
    // the CSRF token before serializing. Failures fall through unchanged.
    private IActionResult HandleTokenIssuingResult(BusinessResult<JwtTokenResponse> result)
    {
        if (result.IsSuccess && result.Value != null)
        {
            var augmented = cookieWriter.ApplyCookies(HttpContext, result.Value, cookieConfig);
            return HandleResult<JwtTokenResponse>(BusinessResult.Success(augmented));
        }
        return HandleResult<JwtTokenResponse>(result);
    }
}