using Cleansia.Core.AppServices.Services;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// T-0128 AC3 (covers T-0105 / IDA-SEC-01 AC3/AC4/AC7) — the audience-enforcement + dev-bypass-gone
/// half of the Google claim-binding contract, asserted directly against the production
/// <see cref="GoogleTokenVerifier"/> (the sole seam that calls
/// <c>GoogleJsonWebSignature.ValidateAsync</c>). The handler-level tests
/// (<see cref="GoogleAuthHandlerTests"/>) mock <c>IGoogleTokenVerifier</c> and so only prove the
/// handler fails closed when the verifier returns null (AC5); they cannot prove that verification
/// ALWAYS runs (no <c>IsDevelopment</c> short-circuit) nor that the audience is pinned. These cases
/// close that gap with NO network dependency:
///   - AC3 (audience enforced / fail-closed): an empty/whitespace <c>ClientId</c> makes the required
///     audience unsatisfiable, so the verifier returns <c>null</c> instead of trusting the token.
///   - AC3 (dev bypass gone): even with a configured client id, a forged/garbage token is rejected
///     (returns null) — verification runs unconditionally and there is no environment short-circuit
///     that yields claims. A source-contract guard additionally pins that no <c>IsDevelopment</c>
///     branch survives in the verifier (mirrors the <c>SecurityTokensTests</c> source-guard idiom).
///
/// The full forged-SIGNATURE rejection path (a syntactically valid JWT with a bad RSA signature /
/// mismatched <c>aud</c>) exercises Google's real crypto/JWKS validation and is honestly deferred to
/// the integration suite; these unit cases prove the fail-closed branches the unit harness CAN run.
/// </summary>
public class GoogleTokenVerifierTests
{
    private static GoogleTokenVerifier CreateVerifier(string clientId)
    {
        var config = new Mock<IGoogleConfig>();
        config.SetupGet(c => c.ClientId).Returns(clientId);
        return new GoogleTokenVerifier(config.Object);
    }

    // AC3 — fail closed when no audience is configured: an empty client id leaves the aud check
    // unconstrained, so the verifier MUST reject rather than trust an unverifiable token.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Unconfigured_ClientId_Fails_Closed_Returns_Null(string clientId)
    {
        var verifier = CreateVerifier(clientId);

        var result = await verifier.VerifyAsync("any-token", CancellationToken.None);

        Assert.Null(result);
    }

    // AC3 — verification ALWAYS runs (no IsDevelopment bypass): a forged/garbage token is rejected
    // even when a client id IS configured. The verifier returns null (fail-closed) and never throws,
    // so a caller can never receive claims for an unverifiable token in any environment.
    [Fact]
    public async Task Forged_Token_With_Configured_Audience_Is_Rejected_Returns_Null()
    {
        var verifier = CreateVerifier("real-client-id.apps.googleusercontent.com");

        var result = await verifier.VerifyAsync("not-a-real-google-id-token", CancellationToken.None);

        Assert.Null(result);
    }

    // AC3 — the dev-bypass is GONE: the verifier source carries no IsDevelopment short-circuit.
    // (Matches the literal invocation idiom of SecurityTokensTests so prose/comments can't trip it —
    // here we assert the IsDevelopment IDENTIFIER does not appear at all in the verifier.)
    [Fact]
    public void Verifier_Source_Has_No_IsDevelopment_Bypass()
    {
        var source = File.ReadAllText(LocateAppServicesFile("Services/GoogleTokenVerifier.cs"));

        // The pinned audience is set on the validation settings, and there is no environment bypass.
        Assert.Contains("Audience", source);
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
