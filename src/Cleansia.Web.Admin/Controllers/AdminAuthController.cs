using Cleansia.Config.Authentication;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
[EnableRateLimiting("auth")]
public class AdminAuthController(
    IMediator mediator,
    AuthCookieWriter cookieWriter,
    AuthCookieConfig cookieConfig) : ApiController(mediator)
{
    /// <summary>
    /// Admin-specific login endpoint that validates user has Administrator or Employee role.
    /// Returns 400 Bad Request with InsufficientPrivileges error if user lacks required role.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("Login")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLogin.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleTokenIssuingResult(result);
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
        var enriched = command with { Token = token, RequiredProfile = UserProfile.Administrator, RequiredAudience = JwtAudiences.Admin };
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

    // Credential mutation: covered by the controller-level "auth" rate limit; the handler keys the
    // subject off the session (the command carries no user id).
    [Permission(Policy.CanChangeOwnPassword)]
    [HttpPost("ChangePassword")]
    [ProducesResponseType(typeof(ChangeOwnPassword.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeOwnPassword.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ChangeOwnPassword.Response>(result);
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
