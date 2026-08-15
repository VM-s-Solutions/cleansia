using System.Text.RegularExpressions;
using Cleansia.Config.Services;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// Azure's health probe decides whether to RECYCLE an instance, so what it polls is a restart policy
/// rather than a monitoring choice.
///
/// <para><b>The incident this pins.</b> On 2026-08-15 <c>healthCheckPath</c> was <c>/health</c>, which
/// runs the dependency checks. A saturated dev Postgres made that probe take <b>95 seconds</b>; App
/// Service recycled the instance; the restart cold-started onto the same contended B2 plan, rebuilt a
/// 70-entity EF model and a fresh connection pool against the same saturated server, and failed the next
/// probe. Both mobile APIs became unusable — and every recycle took capacity from the database that was
/// already short of it.</para>
///
/// <para><b>The distinction, stated once so it is not re-derived.</b> <c>/alive</c> answers "is this
/// process broken" and is the only honest input to a restart. <c>/health</c> answers "are my dependencies
/// reachable" — the right question for the deploy warm loop and for monitoring, and the wrong one to hand
/// a supervisor that responds by killing things. The blob check already reasoned this way ("recycling
/// everything during a storage outage only amplifies it"); it simply was not applied to the database,
/// where the premise was a per-instance wedged pool rather than a shared server with nothing left.</para>
/// </summary>
public sealed class AppServiceHealthProbeTests
{
    [Fact]
    public void The_Azure_Probe_Polls_Liveness_Not_Readiness()
    {
        var bicep = File.ReadAllText(RepoPath("deploy", "bicep", "modules", "appService.bicep"));

        var declared = Regex.Match(bicep, @"param healthCheckPath string = '(?<path>[^']*)'");
        Assert.True(declared.Success, "appService.bicep no longer declares healthCheckPath.");

        Assert.True(
            declared.Groups["path"].Value == "/alive",
            $"The App Service probe defaults to '{declared.Groups["path"].Value}'. It must be '/alive'. "
            + "Azure acts on this by RECYCLING the instance, so pointing it at /health makes a slow shared "
            + "dependency a restart loop — which is what happened on 2026-08-15.");
    }

    /// <summary>
    /// The SSR host is the one deliberate exception and must stay explicit. It has no <c>/alive</c> —
    /// that comes from the .NET <c>MapDefaultEndpoints</c> — and its <c>/health</c> touches no database
    /// and no storage, so it is already liveness by construction.
    /// </summary>
    [Fact]
    public void The_Ssr_Host_Keeps_Its_Own_Probe_Path_Explicitly()
    {
        var main = File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep"));

        Assert.Contains("healthCheckPath: '/health'", main, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "healthCheckPath: '/alive'", main,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A readiness check with no timeout cannot be acted on. The deploy warm loop allows 15 seconds per
    /// probe; unbounded, the database check was measured at 95 and blob at 56, so every caller saw a
    /// timeout rather than an answer.
    /// </summary>
    [Fact]
    public void Readiness_Checks_Are_Bounded_Well_Inside_The_Warm_Loops_Patience()
    {
        Assert.True(
            ReadinessHealthChecks.ReadinessCheckTimeout > TimeSpan.Zero,
            "A zero or negative readiness timeout would fail every check instantly.");

        Assert.True(
            ReadinessHealthChecks.ReadinessCheckTimeout <= TimeSpan.FromSeconds(10),
            $"Readiness checks are bounded at {ReadinessHealthChecks.ReadinessCheckTimeout.TotalSeconds}s. "
            + "The deploy warm loop gives each probe 15s (curl --max-time 15), so anything at or above that "
            + "is a check whose answer no caller ever sees.");
    }

    // Mirrors DeployWarmProbeCoverageTests — walk up to the *.sln, then out of src/ to the repo root.
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
