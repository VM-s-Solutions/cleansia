using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The two halves of "a deployed site is warm before anyone visits it", both of which live outside the
/// solution and neither of which any other test can see.
///
/// <para>The dev warm probe is a COPY of the prod slot probe rather than a shared implementation — six
/// jobs × two loops, twelve near-identical shell blocks. The dangerous part of that copy is not the retry
/// arithmetic, it is the path: the SSR probe hits <c>/</c> and every API probe hits <c>/health</c>, on
/// purpose (<c>/health</c> proves only that Node is listening, while a slot can pass it with a broken
/// Angular engine manifest and 500 every real request). A copy that gets the path wrong warms nothing and
/// still reports green, so the paths are pinned per host and the two loops are pinned against each
/// other.</para>
/// </summary>
public class DeployWarmProbeCoverageTests
{
    private const string SsrSite = "web-cleansia-customer";

    private static readonly Regex SiteProbe = new(
        @"SITE_URL=""https://(?<site>[a-z0-9-]+)-\$\{\{ matrix\.region \}\}-\$\{\{ inputs\.env \}\}\.azurewebsites\.net(?<path>[^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex SlotProbe = new(
        @"SLOT_URL=""https://(?<site>[a-z0-9-]+)-\$\{\{ matrix\.region \}\}-\$\{\{ inputs\.env \}\}-staging\.azurewebsites\.net(?<path>[^""]*)""",
        RegexOptions.Compiled);

    [Fact]
    public void EveryWebHostIsWarmedAfterADevDeploy()
    {
        var probes = Probes(SiteProbe);

        Assert.Equal(
            ["api-cleansia-admin", "api-cleansia-customer", "api-cleansia-customer-mobile",
             "api-cleansia-partner", "api-cleansia-partner-mobile", SsrSite],
            probes.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheSsrProbeRendersAndTheApiProbesDoNot()
    {
        foreach (var (site, path) in Probes(SiteProbe))
        {
            var expected = site == SsrSite ? "/" : "/health";
            Assert.True(path == expected,
                $"The dev warm probe for {site} hits '{path}'; it must hit '{expected}'. The SSR probe forces a " +
                "real render because /health answers 200 from a host whose render path is broken.");
        }
    }

    [Fact]
    public void TheDevProbeAndTheProdSlotProbeAgreeOnEveryPath()
    {
        var dev = Probes(SiteProbe);
        var slot = Probes(SlotProbe);

        Assert.Equal(dev.Keys.Order(StringComparer.Ordinal), slot.Keys.Order(StringComparer.Ordinal));

        foreach (var (site, path) in dev)
        {
            Assert.True(path == slot[site],
                $"The dev probe for {site} hits '{path}' but the prod slot probe hits '{slot[site]}'. Two warm " +
                "loops that disagree on the path are the drift the copy was warned about.");
        }
    }

    /// <summary>
    /// Always On is what keeps an idle dev host from unloading after ~20 minutes, and it was previously
    /// fenced behind <c>env == 'prod'</c>. It costs nothing on a plan already billed by the hour, so the
    /// regression to guard is a well-meant reinstatement of that conditional as a "dev cost posture".
    /// </summary>
    [Fact]
    public void EveryWebHostIsAlwaysOnInEveryStage()
    {
        var bicep = File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep"));

        var settings = Regex.Matches(bicep, @"^\s*alwaysOn:\s*(?<value>.+)$", RegexOptions.Multiline)
            .Select(match => match.Groups["value"].Value.Trim())
            .ToList();

        Assert.True(settings.Count >= 2,
            $"main.bicep sets alwaysOn {settings.Count} time(s) — expected one per web-host module (the API loop and the SSR).");
        Assert.All(settings, value => Assert.Equal("true", value));
    }

    private static Dictionary<string, string> Probes(Regex pattern)
    {
        var workflow = File.ReadAllText(RepoPath(".github", "workflows", "deploy-azure.yml"));

        return pattern.Matches(workflow).ToDictionary(
            match => match.Groups["site"].Value,
            match => match.Groups["path"].Value,
            StringComparer.Ordinal);
    }

    // Mirrors StartupSeedScriptSyncTests — walk up to the *.sln, then out of src/ to the repo root.
    private static string RepoPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");

        var path = Path.GetFullPath(Path.Combine([directory!.FullName, "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected deploy artifact not found: {path}");
        return path;
    }
}
