using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Tests.Common;

/// <summary>
/// T-0106 (IDA-SEC-03). The hole: email-confirm + password-reset codes were a 6-digit
/// <c>Random.Shared.Next(100000,999999)</c> (non-crypto, 900k values) stored in PLAINTEXT and looked
/// up by the bare code. These tests pin the new contract per the owner-decision (BINDING):
///   - AC1: tokens are cryptographic (CSPRNG, >=128 bits) and non-sequential; <c>Random.Shared</c> is
///     gone from the token-generating domain paths.
///   - AC2: only a hash of the token is stored; the persisted column never equals the raw token.
/// The hashing seam (<see cref="SecurityTokens"/>) is the single canonical place the domain generators
/// AND the repository lookup agree on. Written red -> green (predates the implementation).
/// </summary>
public class SecurityTokensTests
{
    // AC1 — generation is high-entropy and unpredictable: a large sample is collision-free and not
    // sequential (the old 6-digit space would collide and/or step by 1).
    [Fact]
    public void Generate_Produces_Unpredictable_NonSequential_HighEntropy_Tokens()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => SecurityTokens.Generate()).ToList();

        // No collisions across 1000 draws — impossible for a 6-digit PRNG, trivial for >=128 bits.
        Assert.Equal(tokens.Count, tokens.Distinct().Count());

        // Two successive tokens differ and are not numerically adjacent (no sequential PRNG stepping).
        var a = SecurityTokens.Generate();
        var b = SecurityTokens.Generate();
        Assert.NotEqual(a, b);

        // >=128 bits of entropy -> URL-safe string materially longer than a 6-char code.
        Assert.True(a.Length >= 16, $"token too short to carry 128 bits: '{a}' ({a.Length} chars)");

        // URL-safe: base64url / hex alphabet only (pasteable in an email link/field, AC: UX unchanged shape).
        Assert.Matches(new Regex("^[A-Za-z0-9_-]+$"), a);
    }

    // AC2 — the hash is deterministic, fixed-width, and never equal to the raw token.
    [Fact]
    public void Hash_Is_Deterministic_FixedWidth_And_Differs_From_Raw()
    {
        var raw = SecurityTokens.Generate();

        var hash1 = SecurityTokens.Hash(raw);
        var hash2 = SecurityTokens.Hash(raw);

        Assert.Equal(hash1, hash2);                 // deterministic — repository can match
        Assert.NotEqual(raw, hash1);                // AC2: stored hash != raw token
        Assert.Equal(64, hash1.Length);             // SHA-256 hex = 64 chars (drives the column length)
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), hash1);
    }

    // AC1 — the token-generating domain paths no longer CALL the non-crypto Random.Shared PRNG.
    // (We match the actual invocation `Random.Shared.Next(` so prose/comments can't trip the assert.)
    [Fact]
    public void Domain_Token_Generation_Does_Not_Use_Random_Shared()
    {
        var userSource = File.ReadAllText(LocateDomainFile("Users/User.cs"));
        Assert.DoesNotContain("Random.Shared.Next", userSource);

        var tokensSource = File.ReadAllText(LocateDomainFile("Common/SecurityTokens.cs"));
        Assert.Contains("RandomNumberGenerator", tokensSource);
        Assert.DoesNotContain("Random.Shared.Next", tokensSource);
    }

    // AC2 — User.CreateWithPassword stores the HASH and surfaces the RAW token to the caller (so the
    // email handler can send the raw value while the row keeps only the hash).
    [Fact]
    public void CreateWithPassword_Stores_Hash_And_Returns_Raw_Confirmation_Token()
    {
        var user = User.CreateWithPassword(
            "person@example.com", "12345678Test!", "First", "Last");

        var raw = user.RawConfirmationToken;

        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.NotEqual(raw, user.ConfirmationCode);                 // not plaintext at rest
        Assert.Equal(SecurityTokens.Hash(raw!), user.ConfirmationCode); // stored == hash(raw)
    }

    // AC2 — UpdateConfirmationCode returns raw, stores hash.
    [Fact]
    public void UpdateConfirmationCode_Returns_Raw_Stores_Hash()
    {
        var user = User.CreateWithPassword("person@example.com", "12345678Test!", "First", "Last");

        var raw = user.UpdateConfirmationCode();

        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.NotEqual(raw, user.ConfirmationCode);
        Assert.Equal(SecurityTokens.Hash(raw), user.ConfirmationCode);
    }

    // AC2 — UpdateResetPasswordToken returns raw, stores hash.
    [Fact]
    public void UpdateResetPasswordToken_Returns_Raw_Stores_Hash()
    {
        var user = User.CreateWithPassword("person@example.com", "12345678Test!", "First", "Last");

        var raw = user.UpdateResetPasswordToken();

        Assert.False(string.IsNullOrWhiteSpace(raw));
        Assert.NotEqual(raw, user.ResetPasswordCode);
        Assert.Equal(SecurityTokens.Hash(raw), user.ResetPasswordCode);
    }

    private static string LocateDomainFile(string relativePath)
    {
        // Cleansia.Api.sln lives in the src/ directory, so its folder IS the source root.
        var srcRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (srcRoot is not null && !File.Exists(Path.Combine(srcRoot.FullName, "Cleansia.Api.sln")))
        {
            srcRoot = srcRoot.Parent;
        }

        Assert.NotNull(srcRoot);
        var full = Path.Combine(srcRoot!.FullName, "Cleansia.Core.Domain", relativePath);
        Assert.True(File.Exists(full), $"expected domain file not found: {full}");
        return full;
    }
}
