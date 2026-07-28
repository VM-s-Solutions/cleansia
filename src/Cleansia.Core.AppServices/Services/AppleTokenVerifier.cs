using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The sole adapter that verifies a Sign in with Apple identity token (ADR-0001 S1 server-truth-identity)
/// — the Apple analogue of <see cref="GoogleTokenVerifier"/>. Verification ALWAYS runs (no environment
/// bypass). The signature is validated with the vetted <see cref="JsonWebTokenHandler"/> against Apple's
/// JWKS (discovered from Apple's OIDC metadata document and cached via
/// <see cref="ConfigurationManager{T}"/>, refreshed on an unknown <c>kid</c>), pinned to RS256 (alg:none
/// and HS256/symmetric key-confusion are rejected). The audience is pinned to an exact-match OR-list of
/// the audiences THIS host configures — the native bundle id and/or the web Services ID, never a wildcard
/// — the issuer to <c>https://appleid.apple.com</c>, and the lifetime (exp/iat) is enforced. The request
/// nonce is bound server-side
/// (<c>SHA256(rawNonce) == token.nonce</c>) to defeat replay. On ANY failure (forged/expired/wrong-aud/
/// wrong-iss signature, unknown kid, JWKS-fetch failure, nonce mismatch, or no configured audience at all
/// which makes the audience check unsatisfiable) it returns <c>null</c> so the caller fails closed with a
/// uniform rejection (S4 — no enumeration leak).
/// </summary>
public class AppleTokenVerifier : IAppleTokenVerifier
{
    private const string AppleIssuer = "https://appleid.apple.com";

    // Apple's OIDC DISCOVERY document, HARDCODED to HTTPS with no config override and no cross-host
    // redirect (so the verifier cannot be pointed at an attacker-controlled key set — no SSRF /
    // key-substitution). It MUST be the discovery document and not Apple's bare key set:
    // OpenIdConnectConfigurationRetriever loads signing keys only through the document's jwks_uri, so a
    // raw key set produces a configuration with zero keys and rejects every token as unsigned.
    private const string AppleMetadataAddress = "https://appleid.apple.com/.well-known/openid-configuration";

    private readonly IAppleConfig _appleConfig;
    private readonly ILogger<AppleTokenVerifier> _logger;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public AppleTokenVerifier(IAppleConfig appleConfig, ILogger<AppleTokenVerifier> logger)
    {
        _appleConfig = appleConfig;
        _logger = logger;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            AppleMetadataAddress,
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<AppleVerifiedClaims?> VerifyAsync(string identityToken, string rawNonce, CancellationToken cancellationToken)
    {
        // Each host accepts only the audiences it configures, and that set is the whole of the isolation
        // between the native and the web flow: a cookie-issuing web host that also listed the native
        // BundleId would let a captured iOS (identityToken, rawNonce) pair mint a browser session. So the
        // list is built from whatever THIS host set, and a blank entry contributes nothing rather than
        // widening the check.
        string[] audiences = [.. new[] { _appleConfig.BundleId, _appleConfig.WebServicesId }
            .Where(audience => !string.IsNullOrWhiteSpace(audience))];

        // Fail closed when the host configured neither: an empty audience list would otherwise leave the
        // aud check effectively unconstrained.
        if (audiences.Length == 0)
        {
            _logger.LogError(
                "Apple identity-token verification failed closed: no Apple audience is configured, so the " +
                "audience check is unsatisfiable. Set Apple:BundleId (native hosts) or Apple:WebServicesId " +
                "(customer web host) — never both on one host.");
            return null;
        }

        try
        {
            // Fetch under the caller's token so a discovery/JWKS outage fails closed HERE, with its cause
            // logged, instead of surfacing as an opaque signature failure. The manager is then handed to
            // the validator (rather than a snapshot of its keys) so an unknown kid — Apple rotates its
            // signing keys — triggers an automatic refresh mid-validation instead of a hard rejection.
            await _configurationManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = AppleIssuer,
                ValidateIssuer = true,
                // An exact-string OR-list, not a widening: no wildcard, no substring match, and
                // ValidateAudience stays on — an audience outside the list is still rejected.
                ValidAudiences = audiences,
                ValidateAudience = true,
                ConfigurationManager = _configurationManager,
                ValidateIssuerSigningKey = true,
                // Pin RS256 so a token whose header advertises alg:none or a symmetric alg (HS256
                // key-confusion against the public JWKS key) is rejected.
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true
            };

            var handler = new JsonWebTokenHandler();
            var result = await handler.ValidateTokenAsync(identityToken, validationParameters);
            if (!result.IsValid)
            {
                // Signature/issuer/audience/lifetime/alg rejection. Log the exception TYPE only (e.g.
                // SecurityTokenExpiredException, SecurityTokenInvalidAudienceException) — never the token —
                // so a genuine verify failure is diagnosable on DEV instead of surfacing as "session expired".
                _logger.LogWarning(
                    "Apple identity-token validation failed: {Failure}",
                    result.Exception?.GetType().Name ?? "invalid token");
                return null;
            }

            var token = (JsonWebToken)result.SecurityToken;

            // Anti-replay: Apple echoes back the nonce the client sent in request.nonce (which is the
            // lowercase hex SHA-256 of the raw nonce). Recompute it from the raw nonce the client POSTed
            // and require an exact match against the token's nonce claim. An encoding/case mismatch here is
            // a silent fail-closed (covered by a known-vector test).
            if (!token.TryGetClaim("nonce", out var nonceClaim) ||
                !FixedTimeEquals(nonceClaim.Value, HashNonce(rawNonce)))
            {
                _logger.LogWarning("Apple identity-token rejected: nonce claim missing or does not match SHA256(rawNonce).");
                return null;
            }

            var subject = token.Subject;
            if (string.IsNullOrEmpty(subject))
            {
                _logger.LogWarning("Apple identity-token rejected: missing subject claim.");
                return null;
            }

            // Apple sends the email claim ONLY on the user's FIRST authorization for this app; every later
            // sign-in carries the sub alone. Requiring it here would lock every returning user out, so the
            // claim is optional and the caller resolves the account by the sub (see AppleAuth.Handler).
            var email = token.TryGetClaim("email", out var emailClaim) && !string.IsNullOrWhiteSpace(emailClaim.Value)
                ? emailClaim.Value
                : null;

            var emailVerified = email is not null &&
                token.TryGetClaim("email_verified", out var emailVerifiedClaim) &&
                IsTrue(emailVerifiedClaim.Value);

            return new AppleVerifiedClaims(subject, email, emailVerified);
        }
        catch (Exception ex)
        {
            // JWKS fetch failure, malformed token, transient network — fail closed, but make the cause
            // visible (type + message, never the token) so it is distinguishable from a clean rejection.
            _logger.LogWarning(ex, "Apple identity-token verification threw and failed closed.");
            return null;
        }
    }

    // Apple represents email_verified as either the boolean true or the string "true".
    private static bool IsTrue(string value)
        => bool.TryParse(value, out var parsed) && parsed;

    private static string HashNonce(string rawNonce)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawNonce));
        return Convert.ToHexStringLower(hash);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
