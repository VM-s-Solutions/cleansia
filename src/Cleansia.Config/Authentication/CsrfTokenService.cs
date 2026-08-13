using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Cleansia.Config.Authentication;

/// <summary>
/// Stateless CSRF-token derivation: an HMAC of the JWT's session claim, keyed by the server secret, so
/// both sides derive the same value and no per-session storage is needed.
///
/// <para>A cross-site attacker can make the browser send the HttpOnly cookie but cannot read it or
/// compute the header. SameSite catches most CSRF alone; <b>this is defence in depth for what SameSite
/// misses.</b> → /architecture/security-rules</para>
/// </summary>
public class CsrfTokenService
{
    private readonly byte[] _secret;

    public CsrfTokenService(string secret)
    {
        if (string.IsNullOrEmpty(secret))
            throw new ArgumentException("Csrf:Secret must be configured", nameof(secret));
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    /// <summary>
    /// Compute the CSRF token for the given session-identifying value.
    /// Typically called with the JWT's <c>jti</c> claim (or <c>sub</c> when
    /// jti is absent — the JWT issuer ought to include jti for unique-token
    /// auditability, but the derivation works either way).
    /// </summary>
    public string Derive(string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey))
            return string.Empty;
        using var hmac = new HMACSHA256(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sessionKey));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Extract the session-identifying value from a validated principal.
    /// Prefers <c>jti</c>; falls back to <c>sub</c>. Returns null if neither
    /// is present (the principal is unauthenticated or malformed).
    /// </summary>
    public static string? GetSessionKey(ClaimsPrincipal principal)
    {
        var jti = principal.FindFirst("jti")?.Value;
        if (!string.IsNullOrEmpty(jti)) return jti;
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
    }
}
