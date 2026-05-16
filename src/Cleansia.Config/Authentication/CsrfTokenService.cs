using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Cleansia.Config.Authentication;

/// <summary>
/// Stateless CSRF-token derivation. The token is an HMAC-SHA256 of the JWT's
/// session-identifying claim (jti, or sub as fallback), keyed by the
/// server's `Csrf:Secret` configuration value. Both sides (server +
/// authenticated client) can derive the same value from the same JWT, so
/// no per-session storage is required.
///
/// Threat model the token defeats: a cross-site attacker can trick the
/// browser into sending the HttpOnly auth cookie on a forged request, but
/// cannot read the cookie or inspect the JWT — so they can't compute the
/// expected CSRF header value. SameSite=Strict catches most CSRF on its
/// own; this is defense-in-depth for the cases SameSite misses
/// (subdomain fronting, certain browser bugs).
///
/// The secret is the same for all instances of a host. Compromise of the
/// secret would let an attacker mint valid CSRF headers for any session
/// whose JWT they already have — but at that point they already have the
/// session, so the CSRF defence is moot.
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
