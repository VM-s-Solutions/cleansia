using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Google.Apis.Auth;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The sole adapter over <see cref="GoogleJsonWebSignature.ValidateAsync(string, GoogleJsonWebSignature.ValidationSettings)"/>
/// (ADR-0001 S1 server-truth-identity). Verification ALWAYS runs — there is no
/// environment bypass — and the audience is pinned to the configured Google OAuth client id so a token
/// minted for a different client is rejected. On ANY failure (forged/expired/wrong-audience signature,
/// or a missing/empty client-id that makes the audience check unsatisfiable) it returns <c>null</c> so
/// the caller fails closed with a uniform rejection (S4 — no enumeration leak).
/// </summary>
public class GoogleTokenVerifier(IGoogleConfig googleConfig) : IGoogleTokenVerifier
{
    public async Task<GoogleVerifiedClaims?> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        // Fail closed when no client id is configured: an empty/whitespace audience would otherwise
        // leave the aud check effectively unconstrained.
        if (string.IsNullOrWhiteSpace(googleConfig.ClientId))
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleConfig.ClientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);
            if (payload is null || string.IsNullOrEmpty(payload.Subject) || string.IsNullOrEmpty(payload.Email))
            {
                return null;
            }

            return new GoogleVerifiedClaims(payload.Subject, payload.Email, payload.EmailVerified);
        }
        catch
        {
            return null;
        }
    }
}
