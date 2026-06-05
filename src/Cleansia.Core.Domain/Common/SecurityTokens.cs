using System.Security.Cryptography;
using System.Text;

namespace Cleansia.Core.Domain.Common;

/// <summary>
/// The single canonical seam for the email-confirmation + password-reset secret tokens
/// (T-0106 / IDA-SEC-03). Both the domain generators (<see cref="Users.User"/>) and the repository
/// lookup MUST agree on the same generation + hashing algorithm, so it lives in one place.
///
/// Contract (owner-decision, BINDING):
///   - <see cref="Generate"/> produces a high-entropy (&gt;=128-bit) URL-safe token via
///     <see cref="RandomNumberGenerator"/> — NEVER <c>Random.Shared</c>. The RAW token is what the
///     user receives by email.
///   - <see cref="Hash"/> is the deterministic SHA-256 (hex) of the raw token. Only the HASH is
///     persisted; the raw token is never stored and never logged (S4 never-leak, S6 logging).
///
/// Mirrors the established refresh-token mechanics in
/// <c>Cleansia.Core.AppServices.Services.RefreshTokenService</c> (same byte source + hex hashing).
/// </summary>
public static class SecurityTokens
{
    // 16 bytes = 128 bits of entropy → 22 base64url chars. The floor required by the ticket; a single
    // token is brute-force-proof on its own (does not lean on the auth rate limiter for safety).
    private const int TokenByteLength = 16;

    /// <summary>
    /// Generates a fresh, cryptographically-random, URL-safe raw token (&gt;=128 bits). This is the
    /// value emailed to the user; it is NEVER persisted — store <see cref="Hash"/> of it instead.
    /// </summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenByteLength);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Deterministic SHA-256 (lowercase hex, 64 chars) of the raw token. Used both when persisting a
    /// freshly-generated token and when resolving an incoming raw token to a stored hash. A hash, not
    /// the raw token, is what lives in the database column.
    /// </summary>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
