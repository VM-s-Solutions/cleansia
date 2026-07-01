namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The single seam that verifies a Google ID-token server-side and returns the VERIFIED claims
/// (ADR-0001 / S1 server-truth-identity, D5 "don't trust client identity"). It is the ONLY place that
/// calls <c>Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync</c>; the implementation pins the
/// audience to the configured Google OAuth client id and ALWAYS runs verification (no environment
/// bypass). Returns <c>null</c> on any failure (forged/expired/wrong-audience/unconfigured) so callers
/// fail closed with a uniform rejection (S4).
/// </summary>
public interface IGoogleTokenVerifier
{
    Task<GoogleVerifiedClaims?> VerifyAsync(string token, CancellationToken cancellationToken);
}

/// <summary>
/// The subset of verified Google ID-token claims the auth flow trusts: <paramref name="Subject"/> is
/// the Google <c>sub</c> (the stable account id, bound as <c>User.GoogleId</c>), <paramref name="Email"/>
/// is the <c>email</c> claim, and <paramref name="EmailVerified"/> is Google's <c>email_verified</c>
/// flag. These are the source of truth — never the client-supplied request fields. Provisioning is gated
/// on <paramref name="EmailVerified"/>.
/// </summary>
public record GoogleVerifiedClaims(string Subject, string Email, bool EmailVerified);
