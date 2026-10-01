using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The two halves of "a deployed site is warm before anyone visits it", both of which live outside the
/// solution and neither of which any other test can see.
///
/// <para>DEV deploys the five APIs as one matrix job (<c>deploy-api</c>) two legs at a time, and each leg
/// warms its own <c>/health</c> before it frees its seat, because six sites cold-starting in parallel on
/// one B2 plan meant whichever deployed last warmed into the worst of that contention — deterministically
/// the two mobile APIs. The SSR is warmed in ONE job (<c>warm-dev-sites</c>) after every deploy. PROD
/// warms each staging slot inside its own leg, because a slot must be proven healthy before it is
/// swapped and that ordering cannot move to the end.</para>
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
    private const string ApiSite = "api-cleansia-${{ matrix.host }}";

    private static readonly Regex SiteProbe = new(
        @"SITE_URL=""https://(?<site>[a-z0-9-]+)-\$\{\{ matrix\.region \}\}-\$\{\{ inputs\.env \}\}\.azurewebsites\.net(?<path>[^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex SlotProbe = new(
        @"SLOT_URL=""https://(?<site>[a-z0-9-]+(?:\$\{\{ matrix\.host \}\})?)-\$\{\{ matrix\.region \}\}-\$\{\{ inputs\.env \}\}-staging\.azurewebsites\.net(?<path>[^""]*)""",
        RegexOptions.Compiled);

    /// <summary>
    /// The API hosts are the legs of one matrix job, and on DEV each leg ends by warming its own
    /// <c>/health</c> — last, so the leg holds its seat until its site answers. The SSR is warmed by
    /// <c>warm-dev-sites</c>. Pin the roster: a host dropped from the matrix is a host nobody deploys or
    /// proves answers.
    /// </summary>
    [Fact]
    public void EveryWebHostIsWarmedAfterADevDeploy()
    {
        var api = Job("deploy-api");

        var roster = Regex.Match(api, @"host: \[(?<hosts>[a-z\-, ]+)\]");
        Assert.True(roster.Success, "deploy-api no longer has a host matrix — did the job change shape?");

        Assert.Equal(
            ["admin", "customer", "customer-mobile", "partner", "partner-mobile"],
            roster.Groups["hosts"].Value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Order(StringComparer.Ordinal));

        Assert.EndsWith(
            "        if: inputs.env != 'prod'\n" +
            "        run: .github/scripts/warm-site.sh \"https://" + ApiSite +
            "-${{ matrix.region }}-${{ inputs.env }}.azurewebsites.net/health\"",
            api.TrimEnd(),
            StringComparison.Ordinal);

        Assert.Contains("warm-site.sh \"https://" + SsrSite + "-", Job("warm-dev-sites"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The SSR is warmed at the end: it must depend on every deploy, or it runs during the contention it
    /// was created to step out of. Needing the matrix job waits for every one of its legs.
    /// </summary>
    [Fact]
    public void TheDevWarmJobWaitsForEveryDeploy()
    {
        var needs = Regex.Match(Job("warm-dev-sites"), @"^    needs: \[(?<needs>[^\]]*)\]", RegexOptions.Multiline);

        Assert.True(needs.Success, "warm-dev-sites no longer lists its needs inline — did the job change shape?");
        foreach (var dependency in new[] { "deploy-api", "deploy-customer-ssr" })
        {
            Assert.Contains(dependency, needs.Groups["needs"].Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The DEV throttle (ADR-0015 D5's fallback): two API legs at a time, so a leg warms beside one other
    /// starting site instead of five, while prod keeps all five so its slot swaps still land together.
    /// Without <c>fail-fast: false</c> one red leg would cancel the rest, which five separate jobs never did.
    /// </summary>
    [Fact]
    public void DevDeploysTheApisTwoAtATimeAndProdAllFive()
    {
        var api = Job("deploy-api");

        Assert.Contains("      fail-fast: false\n", api, StringComparison.Ordinal);
        Assert.Contains("      max-parallel: ${{ inputs.env == 'prod' && 5 || 2 }}\n", api, StringComparison.Ordinal);
    }

    /// <summary>
    /// Still the sharpest edge, now on the prod side: the SSR slot must be warmed at <c>/</c>, because a
    /// slot answers <c>/health</c> from a host whose render path is broken and would then be swapped into
    /// production.
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

    /// <summary>
    /// The prod slot warm runs in the same <c>deploy-api</c> leg as the dev warm, so both cover the leg's
    /// host roster; beside it only the SSR has a slot probe. A per-host copy reappearing next to the
    /// matrix would be an API probe outside that roster.
    /// </summary>
    [Fact]
    public void TheDevLoopAndTheProdSlotLoopCoverTheSameSites()
    {
        Assert.Equal([ApiSite, SsrSite], Probes(SlotProbe).Keys.Order(StringComparer.Ordinal));
        Assert.Contains("SLOT_URL=\"https://" + ApiSite + "-", Job("deploy-api"), StringComparison.Ordinal);
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

    private static string Workflow() =>
        File.ReadAllText(RepoPath(".github", "workflows", "deploy-azure.yml")).ReplaceLineEndings("\n");

    // One job's block: from its key to the next line at job indentation (the next job or section rule).
    private static string Job(string name)
    {
        var job = Regex.Match(
            Workflow(),
            $@"^  {Regex.Escape(name)}:\n.*?(?=^  \S|\z)",
            RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(job.Success, $"deploy-azure.yml has no {name} job.");
        return job.Value;
    }

    private static Dictionary<string, string> Probes(Regex pattern)
    {
        return pattern.Matches(Workflow()).ToDictionary(
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
