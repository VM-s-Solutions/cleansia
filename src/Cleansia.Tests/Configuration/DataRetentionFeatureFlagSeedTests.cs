using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Features.DataRetention;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// That the retention sweep's feature flag is actually SEEDED, and seeded on.
///
/// <para><b>The failure this pins is silent by construction.</b>
/// <c>AppConfigurationProvider.IsFeatureEnabledAsync</c> ends in
/// <c>globalFlag?.IsEnabled ?? false</c> — an absent flag is a DISABLED flag, indistinguishable from one
/// somebody turned off on purpose. <c>DataRetentionJobEnabled</c> was never in the seed, so on every
/// fresh database <c>DataRetentionBackgroundService</c> returned at its first line, logged
/// <i>"disabled by feature flag"</i>, and reported success. All seven of its tasks had never run once:
/// expired user codes, stale devices, old GDPR requests, <b>order customer PII</b>, withdrawn consents,
/// superseded documents and notifications.</para>
///
/// <para>Nothing else can notice this. The timer fires, the handler succeeds, the log line is
/// informational, and the only symptom is data that should have aged out and did not — which is a
/// compliance obligation rather than a housekeeping preference, and is invisible until somebody asks.
/// So the assertion is on the seed text itself, which is the thing that was missing.</para>
/// </summary>
public class DataRetentionFeatureFlagSeedTests
{
    [Fact]
    public void The_Retention_Flag_Is_Seeded_And_Enabled()
    {
        var row = FeatureFlagRow(RetentionDefaults.FeatureFlagName);

        Assert.False(
            string.IsNullOrEmpty(row),
            $"'{RetentionDefaults.FeatureFlagName}' has no row in the FeatureFlags seed. An absent flag "
                + "resolves to FALSE, so the whole retention sweep silently does nothing on a fresh "
                + "database — including anonymising customer PII on old orders.");

        // The flag must be seeded ON, not merely present: a row with IsEnabled = false reproduces the
        // exact silence this class exists to prevent, while looking deliberate to a reader.
        Assert.Contains("true, 'global', NULL)", row, StringComparison.Ordinal);
    }

    /// <summary>
    /// The constant and the seed are two copies of one string, in different languages, and nothing but
    /// this compares them. A rename on the C# side that missed the SQL would re-open the defect above
    /// while every test that mocks the provider stayed green.
    /// </summary>
    [Fact]
    public void The_Seeded_Name_Matches_The_Constant_The_Service_Reads()
    {
        Assert.Equal("DataRetentionJobEnabled", RetentionDefaults.FeatureFlagName);
        Assert.Contains($"'{RetentionDefaults.FeatureFlagName}'", Statements(), StringComparison.Ordinal);
    }

    /// <summary>Anti-vacuity: prove the reader can see the block at all before trusting a hit in it.</summary>
    [Fact]
    public void The_Feature_Flag_Block_Is_Readable()
    {
        Assert.True(
            Regex.Matches(Statements(), @"'global', NULL\)").Count >= 6,
            "The FeatureFlags seed block got smaller than the reader expects — check the seed before "
                + "trusting anything else in this class.");
    }

    // A seeded row wraps across lines, so the row is the matching line plus what follows it up to the
    // value terminator — reading only the matching line would miss the IsEnabled/Scope tail entirely,
    // which is the half this class exists to assert.
    private static string FeatureFlagRow(string name)
    {
        var lines = Statements().Split('\n');
        var start = Array.FindIndex(lines, line => line.Contains($"'{name}'", StringComparison.Ordinal));
        if (start < 0)
        {
            return string.Empty;
        }

        return string.Join(' ', lines.Skip(start).Take(3).Select(line => line.Trim()));
    }

    private static string Statements() =>
        string.Join('\n', Seed().Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

    // Mirrors CountryInvoiceLegalNoticeSeedTests — walk up to the *.sln, then across to the script.
    private static string Seed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the solution directory from the test base directory.");
        return File.ReadAllText(Path.GetFullPath(Path.Combine(dir!.FullName, "..", "sql-scripts", "insert_seed_data.sql")));
    }
}
