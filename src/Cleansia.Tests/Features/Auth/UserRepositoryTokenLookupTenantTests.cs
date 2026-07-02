using System.Text.RegularExpressions;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The anonymous confirm hashed-token lookups must NOT bypass the
/// global tenant filter with <c>IgnoreQueryFilters()</c> (which would let a hashed token match
/// cross-tenant). The repo does expose deliberate bypasses — the <c>*IgnoringTenant*</c> lookups for
/// system triggers and the anonymous login path, plus the login-lockout / reset-budget charges that
/// must land for tenant-stamped accounts on anonymous requests — but the confirm-token flows must NOT
/// route through them. This source-level guard pins that the confirm-lookup methods stay inside the
/// global filter and that <c>IgnoreQueryFilters</c> appears only in the enumerated bypass methods.
/// </summary>
public class UserRepositoryTokenLookupTenantTests
{
    private static string ReadRepositorySource()
    {
        // Cleansia.Api.sln lives in the src/ directory, so its folder IS the source root.
        var srcRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (srcRoot is not null && !File.Exists(Path.Combine(srcRoot.FullName, "Cleansia.Api.sln")))
        {
            srcRoot = srcRoot.Parent;
        }

        Assert.NotNull(srcRoot);
        var path = Path.Combine(srcRoot!.FullName, "Cleansia.Infra.Database", "Repositories", "UserRepository.cs");
        Assert.True(File.Exists(path), $"UserRepository.cs not found at {path}");
        return File.ReadAllText(path);
    }

    // The actual EF call (leading dot + open paren) — so a method's comment mentioning the word
    // "IgnoreQueryFilters" can't trip the assertion; only a real invocation counts.
    private const string IgnoreCall = @"\.IgnoreQueryFilters\(";

    // The confirm-token lookup methods do not call IgnoreQueryFilters().
    [Fact]
    public void ConfirmationCode_Lookups_Do_Not_Ignore_Tenant_Filter()
    {
        var source = ReadRepositorySource();

        foreach (var method in new[] { "GetByConfirmationCodeAsync", "ExistsWithConfirmationCodeAsync" })
        {
            var body = ExtractMethodBody(source, method);
            Assert.False(Regex.IsMatch(body, IgnoreCall), $"{method} must not call IgnoreQueryFilters()");
        }
    }

    // IgnoreQueryFilters() is only CALLED in the enumerated bypass methods (the named cross-tenant /
    // anonymous-login lookups and the anonymous lockout/reset-budget charges), never the confirm flows.
    // Adding a bypass anywhere else must consciously extend this list.
    [Fact]
    public void IgnoreQueryFilters_Is_Confined_To_The_Enumerated_Bypass_Methods()
    {
        var source = ReadRepositorySource();

        var occurrences = Regex.Matches(source, IgnoreCall).Count;
        var inNamedBypasses = new[]
            {
                "GetByIdIgnoringTenantAsync",
                "GetByEmailIgnoringTenantAsync",
                "ExistsWithEmailIgnoringTenantAsync",
                "RecordFailedLoginAsync",
                "TryChargeResetPasswordCodeAttemptAsync",
                // The OTP confirm branch resolves the account by email (anonymous), so its budget
                // charge must land for tenant-stamped accounts too — mirrors the reset charge above.
                // The confirm-token HASH lookups stay filtered (the test above); only the charge is a
                // bypass.
                "TryChargeConfirmationCodeAttemptAsync",
            }
            .Sum(method => Regex.Matches(ExtractMethodBody(source, method), IgnoreCall).Count);

        Assert.Equal(inNamedBypasses, occurrences);
    }

    // Extracts a single method's brace-balanced body by name.
    private static string ExtractMethodBody(string source, string methodName)
    {
        var sigIndex = source.IndexOf(methodName, StringComparison.Ordinal);
        Assert.True(sigIndex >= 0, $"method {methodName} not found");

        var braceStart = source.IndexOf('{', sigIndex);
        Assert.True(braceStart >= 0, $"opening brace for {methodName} not found");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(braceStart, i - braceStart + 1);
                }
            }
        }

        Assert.Fail($"unbalanced braces while extracting {methodName}");
        return string.Empty;
    }
}
