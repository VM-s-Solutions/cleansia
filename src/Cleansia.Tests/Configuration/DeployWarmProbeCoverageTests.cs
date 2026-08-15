using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The two halves of "a deployed site is warm before anyone visits it", both of which live outside the
/// solution and neither of which any other test can see.
///
/// <para>DEV warms in ONE job (<c>warm-dev-sites</c>) after every deploy, because six sites cold-starting
/// in parallel on one B2 plan meant whichever deployed last warmed into the worst of that contention —
/// deterministically the two mobile APIs, which are last in <c>apiHosts</c>. PROD still warms per host,
/// because a staging slot must be proven healthy before it is swapped and that ordering cannot move to
/// the end.</para>
///
/// <para>The dangerous part of two loops is not the retry arithmetic, it is the path: the SSR probe hits
/// <c>/</c> and every API probe hits <c>/health</c>, on purpose (<c>/</c> forces a real render, and a
/// slot can answer <c>/health</c> with a broken Angular engine manifest while 500-ing every real
/// request). A loop that gets the path wrong warms nothing and still reports green, so the paths are
/// pinned per host and the two loops are pinned against each other.</para>
///
/// <para><b>Warming is not the same question as Azure's restart probe.</b> <c>healthCheckPath</c> is
/// pinned separately in <c>AppServiceHealthProbeTests</c> and points at <c>/alive</c>: warming asks
/// "do the dependencies work before I call this deploy good", while Azure's probe decides whether to
/// RECYCLE. Conflating them is what put both mobile APIs in a restart loop on 2026-08-15.</para>
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

    /// <summary>
    /// The dev loop is now one job that iterates the API hosts by name and warms the SSR site
    /// separately, so the roster is a shell list rather than six copied blocks. Pin the roster: a host
    /// dropped from it is a host nobody proves answers.
    /// </summary>
    [Fact]
    public void EveryWebHostIsWarmedAfterADevDeploy()
    {
        var workflow = File.ReadAllText(RepoPath(".github", "workflows", "deploy-azure.yml"));

        var roster = Regex.Match(workflow, @"for host in (?<hosts>[a-z\- ]+); do");
        Assert.True(roster.Success, "warm-dev-sites no longer iterates a host roster — did the loop change shape?");

        Assert.Equal(
            ["admin", "customer", "customer-mobile", "partner", "partner-mobile"],
            roster.Groups["hosts"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));

        Assert.Contains("web-cleansia-customer", workflow, StringComparison.Ordinal);
    }

    /// <summary>
    /// The whole point of moving warming to the end: it must depend on every deploy, or it runs during
    /// the contention it was created to step out of.
    /// </summary>
    [Fact]
    public void TheDevWarmJobWaitsForEveryDeploy()
    {
        var workflow = File.ReadAllText(RepoPath(".github", "workflows", "deploy-azure.yml"));
        var job = Regex.Match(workflow, @"  warm-dev-sites:.*?\n    if:", RegexOptions.Singleline);

        Assert.True(job.Success, "warm-dev-sites is gone — dev warming must not go back inside the deploy jobs.");
        foreach (var dependency in new[]
                 {
                     "deploy-partner-api", "deploy-admin-api", "deploy-customer-api",
                     "deploy-partner-mobile-api", "deploy-customer-mobile-api", "deploy-customer-ssr",
                 })
        {
            Assert.Contains(dependency, job.Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Still the sharpest edge, now on the prod side where the per-host copies remain: the SSR slot must
    /// be warmed at <c>/</c>, because a slot answers <c>/health</c> from a host whose render path is
    /// broken and would then be swapped into production.
    /// </summary>
    [Fact]
    public void TheSsrProbeRendersAndTheApiProbesDoNot()
    {
        foreach (var (site, path) in Probes(SlotProbe))
        {
            var expected = site == SsrSite ? "/" : "/health";
            Assert.True(path == expected,
                $"The prod slot probe for {site} hits '{path}'; it must hit '{expected}'. The SSR probe forces a " +
                "real render because /health answers 200 from a host whose render path is broken.");
        }
    }

    /// <summary>The dev loop warms the same six sites the prod slot loop does — different shape, same roster.</summary>
    [Fact]
    public void TheDevLoopAndTheProdSlotLoopCoverTheSameSites()
    {
        var workflow = File.ReadAllText(RepoPath(".github", "workflows", "deploy-azure.yml"));
        var slot = Probes(SlotProbe);

        Assert.Equal(
            ["api-cleansia-admin", "api-cleansia-customer", "api-cleansia-customer-mobile",
             "api-cleansia-partner", "api-cleansia-partner-mobile", SsrSite],
            slot.Keys.Order(StringComparer.Ordinal));

        foreach (var site in slot.Keys.Where(s => s != SsrSite))
        {
            Assert.Contains(site.Replace("api-cleansia-", string.Empty), workflow, StringComparison.Ordinal);
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
