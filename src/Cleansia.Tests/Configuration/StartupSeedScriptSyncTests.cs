namespace Cleansia.Tests.Configuration;

/// <summary>
/// A fresh Development boot seeds the database by executing
/// <c>&lt;solutionDir&gt;/Cleansia.Infra.Scripts/SeedData/insert_seed_data.sql</c>
/// (<c>CleansiaStartupBase.SeedDevelopmentData</c>), while the canonical, hand-maintained seed
/// lives at the repo root as <c>sql-scripts/insert_seed_data.sql</c>. The startup copy silently
/// drifted once (it predated the NOT NULL <c>OrderStatusHistory.Sequence</c> column, the
/// ServiceCities variants, and the <c>Orders.CurrentStatus</c> backfill — so a fresh dev boot
/// failed to seed at all). This pin fails the build the moment the two files diverge again.
/// </summary>
public class StartupSeedScriptSyncTests
{
    [Fact]
    public void Startup_Seed_Copy_Is_Byte_Identical_To_The_Canonical_Seed_Script()
    {
        var solutionDir = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.False(solutionDir is null, "Could not locate the solution directory from the test base directory.");

        var startupCopy = Path.Combine(solutionDir!, "Cleansia.Infra.Scripts", "SeedData", "insert_seed_data.sql");
        var canonical = Path.GetFullPath(Path.Combine(solutionDir!, "..", "sql-scripts", "insert_seed_data.sql"));

        Assert.True(File.Exists(startupCopy), $"Startup seed copy not found: {startupCopy}");
        Assert.True(File.Exists(canonical), $"Canonical seed script not found: {canonical}");

        var startupBytes = File.ReadAllBytes(startupCopy);
        var canonicalBytes = File.ReadAllBytes(canonical);
        Assert.True(
            startupBytes.SequenceEqual(canonicalBytes),
            "src/Cleansia.Infra.Scripts/SeedData/insert_seed_data.sql has diverged from " +
            "sql-scripts/insert_seed_data.sql. The startup copy is what a fresh dev boot executes — " +
            "copy the canonical script over it so both stay identical.");
    }

    // Mirrors DatabaseMigrationExtensions.FindSolutionDirectory — walk up until a *.sln is found.
    private static string? FindSolutionDirectory(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
