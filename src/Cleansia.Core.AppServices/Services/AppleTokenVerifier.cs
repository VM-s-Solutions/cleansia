using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Cleansia.Core.AppServices.Services;

/// <summary>
/// The sole adapter that verifies a Sign in with Apple identity token (ADR-0001 S1 server-truth-identity)
/// — the Apple analogue of <see cref="GoogleTokenVerifier"/>. Verification ALWAYS runs (no environment
/// bypass). The signature is validated with the vetted <see cref="JsonWebTokenHandler"/> against Apple's
/// JWKS (fetched and cached via <see cref="ConfigurationManager{T}"/>, refreshed on an unknown
/// <c>kid</c>), pinned to RS256 (alg:none and HS256/symmetric key-confusion are rejected). The audience
/// is pinned to the configured native bundle id, the issuer to <c>https://appleid.apple.com</c>, and the
/// lifetime (exp/iat) is enforced. The request nonce is bound server-side
/// (<c>SHA256(rawNonce) == token.nonce</c>) to defeat replay. On ANY failure (forged/expired/wrong-aud/
/// wrong-iss signature, unknown kid, JWKS-fetch failure, nonce mismatch, or a missing/empty bundle id
/// that makes the audience check unsatisfiable) it returns <c>null</c> so the caller fails closed with a
/// uniform rejection (S4 — no enumeration leak).
/// </summary>
public class AppleTokenVerifier : IAppleTokenVerifier
{
    private const string AppleIssuer = "https://appleid.apple.com";

    // Apple's JWKS endpoint is HARDCODED to HTTPS with no config override and no cross-host redirect
    // (so the verifier cannot be pointed at an attacker-controlled key set — no SSRF / key-substitution).
    private const string AppleJwksUri = "https://appleid.apple.com/auth/keys";

    private readonly IAppleConfig _appleConfig;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public AppleTokenVerifier(IAppleConfig appleConfig)
    {
        _appleConfig = appleConfig;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            AppleJwksUri,
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<AppleVerifiedClaims?> VerifyAsync(string identityToken, string rawNonce, CancellationToken cancellationToken)
    {
        // Fail closed when no audience is configured: an empty/whitespace bundle id would otherwise leave
        // the aud check effectively unconstrained.
        if (string.IsNullOrWhiteSpace(_appleConfig.BundleId))
        {
            return null;
        }

        try
        {
            var openIdConfig = await _configurationManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = AppleIssuer,
                ValidateIssuer = true,
                ValidAudience = _appleConfig.BundleId,
                ValidateAudience = true,
                IssuerSigningKeys = openIdConfig.SigningKeys,
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
                return null;
            }

            var subject = token.Subject;
            if (string.IsNullOrEmpty(subject) ||
                !token.TryGetClaim("email", out var emailClaim) ||
                string.IsNullOrEmpty(emailClaim.Value))
            {
                return null;
            }

            var emailVerified = token.TryGetClaim("email_verified", out var emailVerifiedClaim) &&
                IsTrue(emailVerifiedClaim.Value);

            return new AppleVerifiedClaims(subject, emailClaim.Value, emailVerified);
        }
        catch
        {
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
