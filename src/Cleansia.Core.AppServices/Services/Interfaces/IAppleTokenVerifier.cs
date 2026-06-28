namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single seam that verifies a Sign in with Apple identity token server-side and returns the
/// VERIFIED claims (ADR-0001 / S1 server-truth-identity, D5 "don't trust client identity"). It is the
/// ONLY place that calls Apple's JWKS/JWT path; the implementation pins the RS256 signature against the
/// JWKS key whose <c>kid</c> matches the token header, pins the audience to the configured native
/// bundle id, enforces the Apple issuer, validates lifetime, and binds the request nonce
/// (<c>SHA256(rawNonce) == token.nonce</c>) — and ALWAYS runs verification (no environment bypass).
/// Returns <c>null</c> on any failure (forged/expired/wrong-audience/wrong-issuer/nonce-mismatch/
/// unknown-kid/JWKS-outage/unconfigured) so callers fail closed with a uniform rejection (S4).
/// </summary>
public interface IAppleTokenVerifier
{
    Task<AppleVerifiedClaims?> VerifyAsync(string identityToken, string rawNonce, CancellationToken cancellationToken);
}

/// <summary>
/// The subset of verified Apple identity-token claims the auth flow trusts: <paramref name="Subject"/>
/// is the Apple <c>sub</c> (the stable account id, bound as <c>User.AppleId</c>), <paramref name="Email"/>
/// is the email claim, and <paramref name="EmailVerified"/> is Apple's <c>email_verified</c> flag.
/// These are the source of truth — never the client-supplied request fields. Provisioning is gated on
/// <paramref name="EmailVerified"/>.
/// </summary>
public record AppleVerifiedClaims(string Subject, string Email, bool EmailVerified);
