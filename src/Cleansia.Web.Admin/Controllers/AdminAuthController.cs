using Cleansia.Config.Authentication;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;
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
    AuthCookieConfig cookieConfig) : CookieAuthApiController(mediator, cookieWriter, cookieConfig)
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
        var enriched = command with { TrustedDeviceToken = RefreshTokenFromCookieOrBody(command.TrustedDeviceToken ?? string.Empty) };
        var result = await Mediator.Send(enriched);

        return HandleTokenIssuingResult(result);
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
            RequiredProfile = UserProfile.Administrator,
            RequiredAudience = JwtAudiences.Admin,
        };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleTokenIssuingResult(result);
    }

    [Authorize]
    [HttpPost("Logout")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] Logout.Command command, CancellationToken cancellationToken)
    {
        var enriched = command with { Token = RefreshTokenFromCookieOrBody(command.Token) };
        var result = await Mediator.Send(enriched, cancellationToken);
        ClearAuthCookies();
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
}
