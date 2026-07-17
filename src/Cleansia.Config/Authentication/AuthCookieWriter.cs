using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Microsoft.AspNetCore.Http;

namespace Cleansia.Config.Authentication;

/// <summary>
/// Writes the HttpOnly auth cookies on token-issuing responses. Called by
/// each web host's AuthController (Customer / Partner / Admin) after the
/// MediatR handler returns a fresh <see cref="JwtTokenResponse"/>.
///
/// As of the HttpOnly cookie migration (Step 8), the cookies are the sole
/// carrier of the access + refresh tokens on web responses. The returned
/// response has its <see cref="JwtTokenResponse.Token"/> and
/// <see cref="JwtTokenResponse.RefreshToken"/> blanked so they don't leak
/// in the JSON body — clients must rely on the cookie for auth and the
/// <see cref="JwtTokenResponse.CsrfToken"/> for state-changing requests.
///
/// Mobile clients route through their own controllers and never hit this
/// path — they keep using bearer tokens straight from the response body.
/// </summary>
public class AuthCookieWriter
{
    private readonly CsrfTokenService _csrfTokenService;

    public AuthCookieWriter(CsrfTokenService csrfTokenService)
    {
        _csrfTokenService = csrfTokenService;
    }

    /// <summary>
    /// Set the access + refresh cookies and return the response with its
    /// <see cref="JwtTokenResponse.CsrfToken"/> populated and the body
    /// tokens blanked out. The cookie carries the JWT for subsequent
    /// requests; the CSRF token is the JS-readable half of the
    /// double-submit pair.
    /// </summary>
    public JwtTokenResponse ApplyCookies(
        HttpContext httpContext,
        JwtTokenResponse response,
        AuthCookieConfig config)
    {
        // No body token means the response is an error / unconfirmed-email
        // shape — don't issue cookies in that case.
        if (string.IsNullOrEmpty(response.Token))
        {
            return response;
        }

        var accessExpires = response.RefreshTokenExpiresAt;
        var accessOptions = BuildCookieOptions(config, accessExpires);
        httpContext.Response.Cookies.Append(config.AccessCookieName, response.Token, accessOptions);

        if (!string.IsNullOrEmpty(response.RefreshToken))
        {
            var refreshOptions = BuildCookieOptions(config, response.RefreshTokenExpiresAt);
            httpContext.Response.Cookies.Append(config.RefreshCookieName, response.RefreshToken, refreshOptions);
        }

        var csrfToken = _csrfTokenService.Derive(config.SessionKeyForCsrf(response));
        // Strip the body tokens so the cookie is the sole carrier — keeps
        // the JWT out of any logging/storage path that captures JSON bodies.
        return response with
        {
            Token = string.Empty,
            RefreshToken = null,
            CsrfToken = csrfToken,
        };
    }

    /// <summary>
    /// Reads the refresh token from the request's HttpOnly cookie. Used by
    /// Logout / RefreshToken controllers to populate the command's `Token`
    /// field server-side — the client no longer sends it in the body.
    /// </summary>
    public string? ReadRefreshTokenFromCookie(HttpContext httpContext, AuthCookieConfig config)
    {
        return httpContext.Request.Cookies.TryGetValue(config.RefreshCookieName, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// Clear the auth cookies. Called by Logout so the browser drops them
    /// alongside the server-side refresh-token revocation.
    /// </summary>
    public void ClearCookies(HttpContext httpContext, AuthCookieConfig config)
    {
        var options = BuildCookieOptions(config, expires: null);
        // Delete by setting an expired cookie with the same path/domain.
        httpContext.Response.Cookies.Delete(config.AccessCookieName, options);
        httpContext.Response.Cookies.Delete(config.RefreshCookieName, options);
    }

    private static CookieOptions BuildCookieOptions(AuthCookieConfig config, DateTimeOffset? expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = config.RequireSecure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expires,
        };
    }
}

/// <summary>
/// Per-host cookie configuration. Customer / Partner / Admin each provide
/// their own instance so cookie names don't collide on shared origins.
/// </summary>
public class AuthCookieConfig
{
    public required string AccessCookieName { get; init; }
    public required string RefreshCookieName { get; init; }

    /// <summary>
    /// `true` in production; `false` in development on plain HTTP localhost
    /// (browsers refuse `Secure` cookies on HTTP and the dev session breaks).
    /// </summary>
    public bool RequireSecure { get; init; } = true;

    /// <summary>
    /// Resolves the CSRF session-key from the issued token response. Uses the access token's
    /// <c>jti</c> (unique per issuance) so the derived CSRF token ROTATES with the session rather
    /// than being pinned to the stable user id (T-0356) — matching the validation side, which reads
    /// jti from the same token via <c>CsrfTokenService.GetSessionKey</c>. Falls back to the user id
    /// for a legacy token carrying no jti.
    /// </summary>
    public Func<JwtTokenResponse, string> SessionKeyForCsrf { get; init; }
        = response => ExtractJti(response.Token) ?? response.UserId ?? string.Empty;

    private static string? ExtractJti(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var handler = new JwtSecurityTokenHandler();
        if (!handler.CanReadToken(token))
        {
            return null;
        }

        return handler.ReadJwtToken(token).Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
    }
}
