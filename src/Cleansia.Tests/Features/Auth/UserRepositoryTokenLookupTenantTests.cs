using System.Text.RegularExpressions;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The anonymous confirm/reset hashed-token lookups must NOT bypass the
/// global tenant filter with <c>IgnoreQueryFilters()</c> (which would let a hashed token match
/// cross-tenant). The repo exposes <c>GetByIdIgnoringTenantAsync</c> for system
/// triggers, but the confirm/reset flows must NOT route through it. This source-level guard pins that
/// the confirm-lookup methods stay inside the global filter and that only the explicitly-named
/// cross-tenant method (and the base helper) carry <c>IgnoreQueryFilters</c>.
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

    // IgnoreQueryFilters() is only CALLED in the explicitly-named cross-tenant helper, never the
    // confirm/reset flows. (GetByIdIgnoringTenantAsync is the sole intentional bypass.)
    [Fact]
    public void IgnoreQueryFilters_Is_Confined_To_The_Named_Cross_Tenant_Method()
    {
        var source = ReadRepositorySource();

        var occurrences = Regex.Matches(source, IgnoreCall).Count;
        var inNamedBypass = Regex.Matches(ExtractMethodBody(source, "GetByIdIgnoringTenantAsync"), IgnoreCall).Count;

        Assert.Equal(inNamedBypass, occurrences);
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
