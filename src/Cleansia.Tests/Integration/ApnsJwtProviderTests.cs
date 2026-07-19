using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cleansia.Infra.Clients.Apns;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Tests.Integration;

/// <summary>
/// ADR-0029 D1 — the APNs provider token is an ES256 JWT signed from the team <c>.p8</c> with the BCL
/// <see cref="ECDsa"/> (no JWT NuGet), cached ~50 min and re-minted on age-out / <c>Invalidate</c>
/// (a 403). Pins: the header (alg/kid), payload (iss/iat), a signature that verifies against the P-256
/// key, cache reuse, expiry re-mint, and invalidate-forced re-mint.
/// </summary>
public class ApnsJwtProviderTests
{
    private const string KeyId = "ABC1234567";
    private const string TeamId = "TEAM123456";

    private static (IApnsConfig config, ECDsa key) NewConfig()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var config = new FakeApnsConfig
        {
            Enabled = true,
            KeyId = KeyId,
            TeamId = TeamId,
            PrivateKeyPem = key.ExportPkcs8PrivateKeyPem(),
            CustomerBundleId = "cz.cleansia.customer",
            UseSandbox = true,
        };
        return (config, key);
    }

    [Fact]
    public void Token_Is_A_Three_Part_ES256_Jwt_With_Kid_And_Iss()
    {
        var (config, key) = NewConfig();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var provider = new ApnsJwtProvider(config, new FixedTimeProvider(now));

        var token = provider.GetToken();
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal(KeyId, header.RootElement.GetProperty("kid").GetString());

        using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        Assert.Equal(TeamId, payload.RootElement.GetProperty("iss").GetString());
        Assert.Equal(now.ToUnixTimeSeconds(), payload.RootElement.GetProperty("iat").GetInt64());

        key.Dispose();
    }

    [Fact]
    public void Signature_Verifies_Against_The_P256_Key()
    {
        var (config, key) = NewConfig();
        var provider = new ApnsJwtProvider(config, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var token = provider.GetToken();
        var parts = token.Split('.');
        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64UrlDecode(parts[2]);

        Assert.True(key.VerifyData(
            signingInput, signature, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        key.Dispose();
    }

    [Fact]
    public void Token_Is_Cached_Within_The_Window_And_Not_Reminted()
    {
        var (config, _) = NewConfig();
        var clock = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        var provider = new ApnsJwtProvider(config, clock);

        var first = provider.GetToken();
        clock.Now = clock.Now.AddMinutes(49);
        var second = provider.GetToken();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Token_Is_Reminted_After_The_Cache_Window_Expires()
    {
        var (config, _) = NewConfig();
        var clock = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        var provider = new ApnsJwtProvider(config, clock);

        var first = provider.GetToken();
        clock.Now = clock.Now.AddMinutes(51);
        var second = provider.GetToken();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Invalidate_Forces_A_Remint_On_The_Next_Get()
    {
        var (config, _) = NewConfig();
        var clock = new FixedTimeProvider(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        var provider = new ApnsJwtProvider(config, clock);

        var first = provider.GetToken();
        clock.Now = clock.Now.AddSeconds(1);
        provider.Invalidate();
        var second = provider.GetToken();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Base64Wrapped_Pem_Is_Accepted_The_Same_As_Raw_Pem()
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = key.ExportPkcs8PrivateKeyPem();
        var config = new FakeApnsConfig
        {
            Enabled = true,
            KeyId = KeyId,
            TeamId = TeamId,
            PrivateKeyPem = Convert.ToBase64String(Encoding.UTF8.GetBytes(pem)),
            CustomerBundleId = "cz.cleansia.customer",
        };

        var provider = new ApnsJwtProvider(config, new FixedTimeProvider(DateTimeOffset.UtcNow));
        var parts = provider.GetToken().Split('.');

        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64UrlDecode(parts[2]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        key.Dispose();
    }

    [Fact]
    public void HasUsableKey_Is_True_For_A_Real_Key()
    {
        var (config, key) = NewConfig();
        var provider = new ApnsJwtProvider(config, new FixedTimeProvider(DateTimeOffset.UtcNow));

        Assert.True(provider.HasUsableKey());

        key.Dispose();
    }

    [Fact]
    public void HasUsableKey_Is_False_For_An_Unresolved_KeyVault_Reference_And_Never_Throws()
    {
        var config = new FakeApnsConfig
        {
            Enabled = true,
            KeyId = KeyId,
            TeamId = TeamId,
            // The literal App Service substitutes when the KV secret is not yet seeded — non-empty, but
            // neither PEM nor valid base64, so Convert.FromBase64String throws inside ImportKey.
            PrivateKeyPem = "@Microsoft.KeyVault(SecretUri=https://cleansia-kv.vault.azure.net/secrets/Apns--PrivateKeyPem/)",
            CustomerBundleId = "cz.cleansia.customer",
        };
        var provider = new ApnsJwtProvider(config, new FixedTimeProvider(DateTimeOffset.UtcNow));

        Assert.False(provider.HasUsableKey());
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch { 2 => s + "==", 3 => s + "=", _ => s };
        return Convert.FromBase64String(s);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeApnsConfig : IApnsConfig
    {
        public bool Enabled { get; set; }
        public string KeyId { get; set; } = string.Empty;
        public string TeamId { get; set; } = string.Empty;
        public string PrivateKeyPem { get; set; } = string.Empty;
        public string CustomerBundleId { get; set; } = string.Empty;
        public bool UseSandbox { get; set; }
    }
}
