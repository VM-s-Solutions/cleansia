using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The seeded pay configs are DERIVED from the Services and Packages tables, not enumerated by name.
/// The outcome — no catalogue entry left uncovered — is pinned against real PostgreSQL by
/// <c>SeededCataloguePayCoverageTests</c>; what that cannot see is the shape that keeps the outcome
/// true when the catalogue two hundred lines above grows. A hand-listed set would be correct on the
/// day it was written and would silently leave the next entry unbookable.
/// </summary>
public class PayConfigSeedTests
{
    [Fact]
    public void The_Service_Configs_Are_Selected_From_The_Services_Table()
    {
        var statement = PayConfigInsert("s.\"Id\"");

        Assert.Contains("FROM public.\"Services\" s", statement);
        Assert.Contains("s.\"BasePrice\"", statement);
    }

    [Fact]
    public void The_Package_Configs_Are_Selected_From_The_Packages_Table()
    {
        var statement = PayConfigInsert("p.\"Id\"");

        Assert.Contains("FROM public.\"Packages\" p", statement);
        Assert.Contains("p.\"Price\"", statement);
    }

    /// <summary>
    /// Platform-wide is the whole point: a per-employee row answers for one cleaner and leaves the
    /// entry blank for everybody else, which is the state the gate refuses.
    /// </summary>
    [Theory]
    [InlineData("s.\"Id\"")]
    [InlineData("p.\"Id\"")]
    public void Every_Seeded_Config_Is_Platform_Wide(string targetColumn)
    {
        Assert.DoesNotContain("\"Employees\"", PayConfigInsert(targetColumn));
    }

    [Fact]
    public void The_Seed_Writes_Exactly_Two_Pay_Config_Statements()
    {
        Assert.Equal(2, Regex.Matches(Seed(), "INSERT INTO public\\.\"EmployeePayConfigs\"").Count);
    }

    private static string PayConfigInsert(string targetColumn)
    {
        var sql = Seed();
        var statements = Regex.Matches(sql, "INSERT INTO public\\.\"EmployeePayConfigs\"[\\s\\S]*?;")
            .Select(match => match.Value)
            .ToList();

        Assert.NotEmpty(statements);
        return Assert.Single(statements.Where(s => s.Contains(targetColumn, StringComparison.Ordinal)));
    }

    // Mirrors StartupSeedScriptSyncTests — walk up to the *.sln, then across to the canonical script.
    private static string Seed()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the solution directory from the test base directory.");
        return File.ReadAllText(
            Path.GetFullPath(Path.Combine(dir!.FullName, "..", "sql-scripts", "insert_seed_data.sql")));
    }
}
