using System.Reflection;
using System.Text.RegularExpressions;
using Cleansia.Functions.Functions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace Cleansia.Tests.Functions;

/// <summary>
/// That every <c>%AppSetting%</c> timer schedule actually EXISTS as a Function App application setting
/// in the deployed template.
///
/// <para><b>The history is the point.</b> Eight of the twenty timers declare their schedule as
/// <c>[TimerTrigger("%SomeCron%")]</c>. That token is expanded by the Functions HOST at indexing time,
/// from platform application settings. <c>src/Cleansia.Functions/appsettings.json</c> is loaded by
/// <c>Program.cs</c> into the ISOLATED WORKER's <c>IConfiguration</c> — a different process, a different
/// configuration object, which the host never reads. Until 2026-08-22 nothing in <c>deploy/bicep</c> set
/// a single one of them, so the tokens never resolved, the timer listeners were never created, and those
/// functions simply never ran in Azure: no error, no invocation, no telemetry. The timers with literal
/// crons were unaffected, which is the only reason the split was ever noticed.</para>
///
/// <para><see cref="TimerScheduleConfigTests"/> pins each one BY NAME in <c>[InlineData]</c> and compares
/// it against the worker's own <c>appsettings.json</c> — it cannot see this defect, because it never
/// looks at the deployment, and a newly added tokenized timer is invisible to it until somebody
/// remembers to add a row. That is not hypothetical: the two timers added alongside this class were
/// missed there and caught here. This class discovers the tokens by REFLECTION, so it fails on the next
/// one somebody adds whether or not they remember.</para>
/// </summary>
public class TimerCronSettingsAreDeployedTests
{
    /// <summary>
    /// Anti-vacuity floors. A reflection walk that silently finds nothing reads as a clean tree, which
    /// is the most expensive lie available — so the walk asserts its own reach before it asserts
    /// anything about what it found. Measured 2026-08-22: 20 timers, 8 of them tokenized — two of each
    /// added by the same change that wrote this class, which is why the floors are a floor and not an
    /// equality.
    /// </summary>
    private const int MinimumTimerFunctions = 20;

    private const int MinimumTokenizedTimers = 8;

    private static readonly Regex TokenPattern = new(@"^%(?<name>[A-Za-z0-9_]+)%$", RegexOptions.Compiled);

    [Fact]
    public void Every_tokenized_timer_schedule_has_a_matching_app_setting_in_the_deployed_template()
    {
        var tokenized = TokenizedSchedules();

        Assert.True(
            tokenized.Count >= MinimumTokenizedTimers,
            $"Expected at least {MinimumTokenizedTimers} %AppSetting% timer schedules, found {tokenized.Count}. "
                + "Either the reflection walk stopped seeing the Functions assembly, or the tokens were "
                + "replaced by literals — check before lowering this floor.");

        var deployed = DeployedCronSettings();

        var missing = tokenized
            .Where(entry => !deployed.ContainsKey(entry.Value))
            .Select(entry => $"{entry.Key} -> %{entry.Value}%")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "These timer functions read their schedule from an app setting that deploy/bicep/main.bicep "
                + "never sets, so the token cannot resolve and the timer will NEVER FIRE in Azure:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, missing)
                + Environment.NewLine
                + "Add the key to the `cronSettings` var in main.bicep.");
    }

    /// <summary>
    /// Declaring the var is half the fix — it has to reach the module. A `var` that nothing consumes is
    /// a Bicep WARNING, not an error, so this would otherwise deploy green and change nothing.
    /// </summary>
    [Fact]
    public void The_cron_settings_var_is_passed_to_the_function_app_module()
    {
        var bicep = WithoutComments(File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep")));

        Assert.Matches(@"extraAppSettings:\s*union\([^\r\n]*\bcronSettings\b", bicep);
    }

    /// <summary>
    /// The deployed value and the worker's committed default must agree. They are two copies by
    /// necessity — the host reads one, <see cref="TimerScheduleConfigTests"/> reads the other — so the
    /// only thing standing between them and a silent drift is this assertion.
    /// </summary>
    [Fact]
    public void Deployed_cron_values_match_the_committed_worker_defaults()
    {
        var deployed = DeployedCronSettings();
        var committed = WorkerDefaults();

        var drifted = deployed
            .Where(setting => committed[setting.Key] != setting.Value)
            .Select(setting => $"{setting.Key}: bicep '{setting.Value}' vs appsettings.json '{committed[setting.Key]}'")
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            drifted.Count == 0,
            "deploy/bicep/main.bicep and src/Cleansia.Functions/appsettings.json disagree on a cron. The "
                + "deployed value is the one that runs; the committed one is what every test and reader "
                + "believes. Change both or neither:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, drifted));
    }

    /// <summary>Function type name -> the app-setting NAME its schedule token names (no percent signs).</summary>
    private static Dictionary<string, string> TokenizedSchedules()
    {
        var timerFunctions = typeof(SendNewJobsDigestTimerFunction).Assembly
            .GetTypes()
            .Select(type => (Type: type, Schedule: ScheduleOrNull(type)))
            .Where(candidate => candidate.Schedule is not null)
            .ToList();

        Assert.True(
            timerFunctions.Count >= MinimumTimerFunctions,
            $"Expected at least {MinimumTimerFunctions} timer functions, found {timerFunctions.Count}.");

        return timerFunctions
            .Select(candidate => (candidate.Type, Match: TokenPattern.Match(candidate.Schedule!)))
            .Where(candidate => candidate.Match.Success)
            .ToDictionary(
                candidate => candidate.Type.Name,
                candidate => candidate.Match.Groups["name"].Value,
                StringComparer.Ordinal);
    }

    private static string? ScheduleOrNull(Type functionType)
    {
        var run = functionType.GetMethod("Run", BindingFlags.Public | BindingFlags.Instance);

        var timerParam = run?.GetParameters().FirstOrDefault(p => p.ParameterType == typeof(TimerInfo));

        return timerParam?.GetCustomAttribute<TimerTriggerAttribute>()?.Schedule;
    }

    /// <summary>The keys and values of main.bicep's `cronSettings` var, comments stripped first.</summary>
    private static Dictionary<string, string> DeployedCronSettings()
    {
        var bicep = WithoutComments(File.ReadAllText(RepoPath("deploy", "bicep", "main.bicep")));

        var block = Regex.Match(bicep, @"var\s+cronSettings\s*=\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline);

        Assert.True(block.Success, "Could not find the `cronSettings` var in deploy/bicep/main.bicep.");

        return Regex.Matches(block.Groups["body"].Value, @"(?<key>[A-Za-z0-9_]+)\s*:\s*'(?<value>[^']*)'")
            .ToDictionary(
                entry => entry.Groups["key"].Value,
                entry => entry.Groups["value"].Value,
                StringComparer.Ordinal);
    }

    /// <summary>The same keys as read from the worker's committed appsettings.json.</summary>
    private static Dictionary<string, string> WorkerDefaults()
    {
        var solution = SolutionDirectory();
        var appSettings = Path.Combine(solution, "Cleansia.Functions", "appsettings.json");

        Assert.True(File.Exists(appSettings), $"Committed Functions defaults not found at '{appSettings}'.");

        var configuration = new ConfigurationBuilder().AddJsonFile(appSettings, optional: false).Build();

        return DeployedCronSettings().Keys.ToDictionary(
            key => key,
            key => configuration[key] ?? string.Empty,
            StringComparer.Ordinal);
    }

    // Mirrors DeployWarmProbeCoverageTests — walk up to the *.sln, then out of src/ to the repo root.
    private static string RepoPath(params string[] segments)
    {
        var path = Path.GetFullPath(Path.Combine([SolutionDirectory(), "..", .. segments]));
        Assert.True(File.Exists(path), $"Expected deploy artifact not found: {path}");
        return path;
    }

    private static string SolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !directory.EnumerateFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "Could not locate the solution directory from the test base directory.");
        return directory!.FullName;
    }

    // A Bicep comment naming a setting would otherwise satisfy every regex above without deploying it.
    private static string WithoutComments(string template) =>
        Regex.Replace(
            Regex.Replace(template, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline),
            @"//[^\n]*",
            string.Empty);
}
