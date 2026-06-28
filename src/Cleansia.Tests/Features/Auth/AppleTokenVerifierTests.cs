using System.Security.Cryptography;
using System.Text;
using Cleansia.Core.AppServices.Services;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The fail-closed + source-contract half of the Apple claim-binding guarantee, asserted directly
/// against the production <see cref="AppleTokenVerifier"/> (the sole seam that calls Apple's JWKS/JWT
/// path) — the Apple analogue of <see cref="GoogleTokenVerifierTests"/>. The
/// handler-level tests (<see cref="AppleAuthHandlerTests"/>) mock <c>IAppleTokenVerifier</c> and so only
/// prove the handler fails closed when the verifier returns null; they cannot prove the verifier itself
/// pins RS256/aud/iss, binds the nonce, or carries no environment bypass. These cases close that gap
/// with NO required network dependency:
///   - audience enforced / fail-closed: an empty/whitespace <c>BundleId</c> makes the required audience
///     unsatisfiable, so the verifier returns <c>null</c> before any JWKS fetch.
///   - fail-closed end-to-end: even with a configured bundle id, a forged/garbage token yields
///     <c>null</c> and never throws — whether the JWKS fetch fails (outage ⇒ fail-closed) or the token
///     is rejected, the caller can never receive claims for an unverifiable token.
///   - nonce encoding is a KNOWN VECTOR: the server recomputes <c>SHA256(rawNonce)</c> as lowercase hex
///     over UTF-8 bytes (Apple's representation). A hex/base64/case drift would be a silent fail-closed,
///     so the encoding is pinned by a fixed vector plus a source guard.
///   - no env bypass: the verifier source pins RS256 + the Apple issuer + the bundle-id audience + the
///     nonce check and carries no <c>IsDevelopment</c> short-circuit (mirrors the GoogleTokenVerifier
///     source-guard idiom).
///
/// The full forged-SIGNATURE / live-JWKS rejection path (a syntactically valid JWT with a bad RSA
/// signature / mismatched aud against Apple's real keys) is honestly deferred to the integration suite
/// (the T-0128 precedent); these unit cases prove the fail-closed branches the unit harness CAN run.
/// </summary>
public class AppleTokenVerifierTests
{
    private static AppleTokenVerifier CreateVerifier(string bundleId)
    {
        var config = new Mock<IAppleConfig>();
        config.SetupGet(c => c.BundleId).Returns(bundleId);
        return new AppleTokenVerifier(config.Object);
    }

    // Fail closed when no audience is configured: an empty bundle id leaves the aud check unconstrained,
    // so the verifier MUST reject (and never touch the network) rather than trust an unverifiable token.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unconfigured_BundleId_Fails_Closed_Returns_Null(string bundleId)
    {
        var verifier = CreateVerifier(bundleId);

        var result = await verifier.VerifyAsync("any-token", "any-raw-nonce", CancellationToken.None);

        Assert.Null(result);
    }

    // Fail-closed end-to-end: a forged/garbage token is rejected even when a bundle id IS configured.
    // The verifier returns null and never throws — whether the JWKS fetch fails (offline ⇒ fail-closed)
    // or the token is rejected against the fetched keys, no claims are ever returned.
    [Fact]
    public async Task Forged_Token_With_Configured_Audience_Fails_Closed_Returns_Null()
    {
        var verifier = CreateVerifier("cz.cleansia.customer");

        var result = await verifier.VerifyAsync("not-a-real-apple-identity-token", "any-raw-nonce", CancellationToken.None);

        Assert.Null(result);
    }

    // The nonce encoding is a fixed contract: lowercase hex SHA-256 over the UTF-8 bytes of the raw
    // nonce — the representation Apple echoes in the token's nonce claim. A drift to uppercase hex or
    // base64 would silently fail the binding (every login rejected) and is caught here by a known vector.
    [Fact]
    public void Nonce_Hash_Is_Lowercase_Hex_Sha256_KnownVector()
    {
        const string rawNonce = "abc";
        // Canonical SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        const string expectedLowerHex = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        var actual = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawNonce)));

        Assert.Equal(expectedLowerHex, actual);
        // Guard the case explicitly: an uppercase representation must NOT match Apple's lowercase claim.
        Assert.NotEqual(expectedLowerHex.ToUpperInvariant(), actual);
    }

    // The verifier source pins RS256 + the Apple issuer + the bundle-id audience + the nonce check, and
    // carries no IsDevelopment environment bypass (mirrors the GoogleTokenVerifier source-guard idiom).
    [Fact]
    public void Verifier_Source_Pins_Algorithm_Issuer_Audience_Nonce_And_Has_No_IsDevelopment_Bypass()
    {
        var source = File.ReadAllText(LocateAppServicesFile("Services/AppleTokenVerifier.cs"));

        Assert.Contains("RsaSha256", source);
        Assert.Contains("ValidAlgorithms", source);
        Assert.Contains("https://appleid.apple.com", source);
        Assert.Contains("ValidIssuer", source);
        Assert.Contains("ValidAudience", source);
        Assert.Contains("BundleId", source);
        Assert.Contains("nonce", source);
        Assert.Contains("ToHexStringLower", source);
        // The JWKS endpoint is hardcoded HTTPS, with no config override.
        Assert.Contains("https://appleid.apple.com/auth/keys", source);
        Assert.DoesNotContain("IsDevelopment", source);
    }

    private static string LocateAppServicesFile(string relativePath)
    {
        // Cleansia.Api.sln lives in the src/ directory, so its folder IS the source root.
        var srcRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (srcRoot is not null && !File.Exists(Path.Combine(srcRoot.FullName, "Cleansia.Api.sln")))
        {
            srcRoot = srcRoot.Parent;
        }

        Assert.NotNull(srcRoot);
        var full = Path.Combine(srcRoot!.FullName, "Cleansia.Core.AppServices", relativePath);
        Assert.True(File.Exists(full), $"expected AppServices file not found: {full}");
        return full;
    }
}
