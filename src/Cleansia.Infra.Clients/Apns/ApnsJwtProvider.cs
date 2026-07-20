using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Infra.Clients.Apns;

/// <summary>
/// ES256 provider-token minter for APNs (ADR-0029 D1). Signs <c>{header}.{payload}</c> with the
/// team's P-256 <c>.p8</c> key using the BCL <see cref="ECDsa"/> — no third-party JWT library, no new
/// NuGet. The token is cached ~50 min per Apple's provider-token rules and re-minted on age-out or
/// <see cref="Invalidate"/> (a 403). The signing key material never leaves this type.
/// </summary>
public sealed class ApnsJwtProvider(IApnsConfig config, TimeProvider timeProvider) : IApnsJwtProvider
{
    // Apple accepts a provider token for 20–60 min and rate-limits re-minting; 50 min stays comfortably
    // inside the window while minimizing mint churn.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(50);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // The JWT header/payload keys are literal (alg/kid/iss/iat) — no naming policy needed.
    };

    private readonly Lock _gate = new();
    private string? _cachedToken;
    private DateTimeOffset _mintedAt;

    public string GetToken()
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (_cachedToken is not null && now - _mintedAt < CacheLifetime)
            {
                return _cachedToken;
            }

            _cachedToken = Mint(now);
            _mintedAt = now;
            return _cachedToken;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cachedToken = null;
        }
    }

    public bool HasUsableKey()
    {
        try
        {
            using var probe = ECDsa.Create();
            ImportKey(probe, config.PrivateKeyPem);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            // Neither PEM (no "-----" header) nor valid base64 (an unresolved "@Microsoft.KeyVault(…)"
            // literal fails Convert.FromBase64String), or a corrupt key body ImportFromPem rejects. The
            // client treats this as Skipped, not a crash. S6: swallow the detail — it wraps key material.
            return false;
        }
    }

    private string Mint(DateTimeOffset now)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, string> { ["alg"] = "ES256", ["kid"] = config.KeyId }, JsonOptions));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(
            new Dictionary<string, object> { ["iss"] = config.TeamId, ["iat"] = now.ToUnixTimeSeconds() }, JsonOptions));

        var signingInput = $"{header}.{payload}";

        using var ecdsa = ECDsa.Create();
        ImportKey(ecdsa, config.PrivateKeyPem);

        // ES256 = the raw R||S concatenation (IEEE P1363), NOT the ASN.1/DER form SignData defaults to.
        var signature = ecdsa.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    // Same dual-accept convention as FCM:ServiceAccountJson — a raw PEM (.p8 contents) or a base64
    // wrapper of that PEM text.
    private static void ImportKey(ECDsa ecdsa, string keyMaterial)
    {
        var raw = keyMaterial.Trim();
        var pem = raw.StartsWith("-----", StringComparison.Ordinal)
            ? raw
            : Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        ecdsa.ImportFromPem(pem);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
