using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Cleansia.Core.Domain.Common;

/// <summary>
/// The single canonical seam for the email-confirmation + password-reset secret tokens.
/// Both the domain generators (<see cref="Users.User"/>) and the repository
/// lookup MUST agree on the same generation + hashing algorithm, so it lives in one place.
///
/// Contract (owner-decision, BINDING). There are TWO token classes with different safety models:
///   - <see cref="Generate"/> — high-entropy (&gt;=128-bit) URL-safe token via
///     <see cref="RandomNumberGenerator"/> — NEVER <c>Random.Shared</c>. Self-authenticating: it may
///     be resolved by its hash alone (the password-reset link token). The RAW token is what the
///     user receives by email.
///   - <see cref="GenerateOtp"/> — the 6-digit typed verification code the apps render as six digit
///     boxes (email confirmation). A 10^6 space is NOT self-authenticating: it must NEVER be resolved
///     by the bare code — verification is scoped to the account that requested it (email + code),
///     bounded by the per-code attempt budget and the 15-minute expiry.
///   - <see cref="Hash"/> is the deterministic SHA-256 (hex) of the raw token. Only the HASH is
///     persisted; the raw token is never stored and never logged (S4 never-leak, S6 logging). For the
///     OTP class the at-rest hash is hygiene, not brute-proofing (10^6 SHA-256s are trivial offline) —
///     the expiry + attempt cap + email scoping carry the safety.
///
/// Mirrors the established refresh-token mechanics in
/// <c>Cleansia.Core.AppServices.Services.RefreshTokenService</c> (same byte source + hex hashing).
/// </summary>
public static class SecurityTokens
{
    /// <summary>Exact length of a <see cref="GenerateOtp"/> code. Legacy link tokens are 22 chars, so
    /// length discriminates the two classes on the wire.</summary>
    public const int OtpLength = 6;

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
    /// Generates the 6-digit typed verification code for the email-confirmation flow — uniform over
    /// 000000–999999 via <see cref="RandomNumberGenerator"/> (never <c>Random.Shared</c>), zero-padded
    /// so every value is equally likely and equally shaped. See the class contract: an OTP is never
    /// resolved by the bare code — always (email + code) under the attempt budget and expiry.
    /// </summary>
    public static string GenerateOtp()
        => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

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
