using System.Text.RegularExpressions;

namespace Cleansia.Tests.RateLimiting;

/// <summary>
/// ADR-0003 "How a reviewer verifies" #1 (AC3) and #6 (AC7) — the mechanical source gate.
///
/// These are deliberately SOURCE-LEVEL (grep-as-a-test) rather than reflection: the contract is
/// about how the limiter is <em>registered</em> in the startup pipeline, which reflection over the
/// built DI container cannot prove (a partitioned policy and an un-partitioned named limiter both
/// resolve to a limiter at runtime). The reviewer's gate is literally "<c>AddFixedWindowLimiter(</c>
/// must not appear in any <c>*Startup*.cs</c>", so we assert exactly that against the real tree.
/// </summary>
public class RateLimiterSourceContractTests
{
    /// <summary>The five host project directory names (ADR-0003 #6 keeps the literal list so a
    /// future sixth host cannot be silently skipped).</summary>
    private static readonly string[] HostProjects =
    {
        "Cleansia.Web.Admin",
        "Cleansia.Web.Partner",
        "Cleansia.Web.Customer",
        "Cleansia.Web.Mobile.Partner",
        "Cleansia.Web.Mobile.Customer",
    };

    private static DirectoryInfo SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cleansia.Api.sln")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate src/ root (Cleansia.Api.sln).");
        return dir!;
    }

    private static IEnumerable<string> CsFilesUnder(DirectoryInfo root) =>
        root.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     // Exclude THIS test's own source: it contains the forbidden token in string
                     // literals / assertion messages, which would otherwise self-trip the grep.
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}Cleansia.Tests{Path.DirectorySeparatorChar}RateLimiting{Path.DirectorySeparatorChar}"))
            .Select(f => f.FullName);

    // AC3 / verify #1 — no un-partitioned named limiter remains, anywhere.
    [Fact]
    public void No_AddFixedWindowLimiter_Anywhere_In_Source()
    {
        var src = SrcRoot();
        var offenders = CsFilesUnder(src)
            .Where(f => File.ReadAllText(f).Contains("AddFixedWindowLimiter("))
            .Select(f => Path.GetRelativePath(src.FullName, f))
            .OrderBy(x => x)
            .ToList();

        Assert.True(offenders.Count == 0,
            "ADR-0003 #1 / AC3: AddFixedWindowLimiter( (an un-partitioned named limiter = one global " +
            "bucket) must not appear anywhere. Offending files:\n  " + string.Join("\n  ", offenders));
    }

    // AC3 / verify #1 — the named policies are registered via AddPolicy(name, …) (partitioned).
    [Fact]
    public void Auth_And_Interactive_Registered_Via_AddPolicy_In_StartupBase()
    {
        var src = SrcRoot();
        var baseFile = Path.Combine(src.FullName, "Cleansia.Config", "Abstractions", "CleansiaStartupBase.cs");
        var policiesFile = Path.Combine(src.FullName, "Cleansia.Config", "RateLimiting", "RateLimitPolicies.cs");

        // The partitioned registration may live in the base directly or in the extracted helper it
        // calls; accept either, but it MUST be AddPolicy + GetFixedWindowLimiter.
        var text = (File.Exists(baseFile) ? File.ReadAllText(baseFile) : "")
                 + (File.Exists(policiesFile) ? File.ReadAllText(policiesFile) : "");

        Assert.Contains("AddPolicy", text);
        Assert.Contains("RateLimitPartition.GetFixedWindowLimiter", text);
        // The literal policy names are PRESERVED so existing [EnableRateLimiting("auth"/"interactive")]
        // sites are untouched. They may be passed to AddPolicy directly OR via a named constant whose
        // value is the literal — accept either, but both literals MUST exist and AddPolicy MUST consume
        // the auth/interactive policy (directly or through the constant).
        Assert.Contains("\"auth\"", text);
        Assert.Contains("\"interactive\"", text);
        Assert.Matches(new Regex("AddPolicy\\(\\s*(\"auth\"|AuthPolicy)"), text);
        Assert.Matches(new Regex("AddPolicy\\(\\s*(\"interactive\"|InteractivePolicy)"), text);
    }

    // AC7 / verify #6 — zero per-host overrides of the limiter (by explicit host name).
    [Fact]
    public void No_Host_Project_Redefines_The_Limiter()
    {
        var src = SrcRoot();
        var violations = new List<string>();

        foreach (var host in HostProjects)
        {
            var hostDir = new DirectoryInfo(Path.Combine(src.FullName, host));
            Assert.True(hostDir.Exists, $"Host project directory missing: {host}");

            foreach (var file in CsFilesUnder(hostDir))
            {
                var t = File.ReadAllText(file);
                var rel = Path.GetRelativePath(src.FullName, file);
                if (t.Contains("AddRateLimiter("))
                    violations.Add($"{rel}: AddRateLimiter(");
                if (t.Contains("AddFixedWindowLimiter("))
                    violations.Add($"{rel}: AddFixedWindowLimiter(");
                if (Regex.IsMatch(t, "AddPolicy[^;]*\"(auth|interactive)\""))
                    violations.Add($"{rel}: AddPolicy(\"auth\"/\"interactive\")");
            }
        }

        Assert.True(violations.Count == 0,
            "ADR-0003 #6 / AC7: the limiter is defined ONCE in CleansiaStartupBase; no host may " +
            "re-register it. Violations:\n  " + string.Join("\n  ", violations));
    }
}
