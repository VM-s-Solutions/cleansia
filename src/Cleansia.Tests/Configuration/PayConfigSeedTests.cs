using System.Text.RegularExpressions;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The seeded pay configs are DERIVED from the PRICE ROWS, not enumerated by name. The outcome — no
/// catalogue entry left uncovered — is pinned against real PostgreSQL by
/// <c>SeededCataloguePayCoverageTests</c>; what that cannot see is the shape that keeps the outcome
/// true when the catalogue two hundred lines above grows. A hand-listed set would be correct on the
/// day it was written and would silently leave the next entry unbookable.
///
/// <para>The source moved from the catalogue table to the price table when prices became per-currency,
/// and the row it reads is the reason: pay is a MULTIPLE OF A PRICE, so the multiple and the currency
/// the config is stamped with have to come from the same row. Reading the catalogue and stamping a
/// currency chosen separately is what made the same figure roughly 24x wrong in
/// <c>BulkCreateEmployeePayConfigs</c>. Selecting from <c>ServicePrices</c>/<c>PackagePrices</c> and
/// taking <c>CurrencyId</c> from that same row makes the pair inseparable rather than merely
/// correct.</para>
/// </summary>
public class PayConfigSeedTests
{
    [Fact]
    public void The_Service_Configs_Are_Selected_From_The_Service_Price_Rows()
    {
        var statement = PayConfigInsert("sp.\"ServiceId\"");

        Assert.Contains("FROM public.\"ServicePrices\" sp", statement);
        Assert.Contains("sp.\"BasePrice\"", statement);
        Assert.Contains("sp.\"PerRoomPrice\"", statement);
    }

    [Fact]
    public void The_Package_Configs_Are_Selected_From_The_Package_Price_Rows()
    {
        var statement = PayConfigInsert("pp.\"PackageId\"");

        Assert.Contains("FROM public.\"PackagePrices\" pp", statement);
        Assert.Contains("pp.\"Price\"", statement);
    }

    /// <summary>
    /// The currency comes from the SAME ROW the amount was multiplied out of. Any other source — a
    /// lookup by code beside the join, a parameter — can disagree with it, and the disagreement is
    /// silent: a config stamped CZK holding a EUR-derived number looks exactly like a correct one.
    /// </summary>
    [Theory]
    [InlineData("sp.\"ServiceId\"", "sp.\"CurrencyId\"")]
    [InlineData("pp.\"PackageId\"", "pp.\"CurrencyId\"")]
    public void The_Config_Currency_Is_The_Price_Rows_Own_Currency(string targetColumn, string currencyColumn)
    {
        Assert.Contains(currencyColumn, PayConfigInsert(targetColumn));
    }

    /// <summary>
    /// Platform-wide is the whole point: a per-employee row answers for one cleaner and leaves the
    /// entry blank for everybody else, which is the state the gate refuses.
    /// </summary>
    [Theory]
    [InlineData("sp.\"ServiceId\"")]
    [InlineData("pp.\"PackageId\"")]
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
        return Assert.Single(statements, s => s.Contains(targetColumn, StringComparison.Ordinal));
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
