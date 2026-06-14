using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Config.Authentication;

/// <summary>
/// Shared base for the cookie-carrying web auth controllers (Customer / Partner / Admin). The
/// HttpOnly-cookie machinery — augmenting a token-issuing result with the auth cookies + CSRF token,
/// reading the refresh token cookie-first on Refresh/Logout, and clearing the cookies on Logout — was
/// previously copy-pasted verbatim across the three hosts. It lives here once. Each host keeps
/// only its host-specific actions (command types, the per-host refresh profile/audience pin).
///
/// Native-client hosts (Mobile.Customer / Mobile.Partner) do NOT use cookies — they return bearer
/// tokens straight from the response body — and intentionally do not derive from this base.
/// </summary>
public abstract class CookieAuthApiController(
    IMediator mediator,
    AuthCookieWriter cookieWriter,
    AuthCookieConfig cookieConfig) : CleansiaApiController(mediator)
{
    protected readonly AuthCookieWriter CookieWriter = cookieWriter;
    protected readonly AuthCookieConfig CookieConfig = cookieConfig;

    /// <summary>
    /// Augment a successful token-issuing result with the HttpOnly cookies + the CSRF token before
    /// serializing. Failures fall through unchanged.
    /// </summary>
    protected IActionResult HandleTokenIssuingResult(BusinessResult<JwtTokenResponse> result)
    {
        if (result.IsSuccess && result.Value != null)
        {
            var augmented = CookieWriter.ApplyCookies(HttpContext, result.Value, CookieConfig);
            return HandleResult<JwtTokenResponse>(BusinessResult.Success(augmented));
        }

        return HandleResult<JwtTokenResponse>(result);
    }

    /// <summary>
    /// Cookie-first refresh token read: the HttpOnly cookie wins when present; the body field is
    /// accepted only for back-compat.
    /// </summary>
    protected string RefreshTokenFromCookieOrBody(string bodyToken)
        => CookieWriter.ReadRefreshTokenFromCookie(HttpContext, CookieConfig) ?? bodyToken;

    /// <summary>
    /// Clear the auth cookies. Logout always clears them, even when the server-side revoke failed —
    /// the user pressed sign-out, they expect to be out.
    /// </summary>
    protected void ClearAuthCookies()
        => CookieWriter.ClearCookies(HttpContext, CookieConfig);
}
