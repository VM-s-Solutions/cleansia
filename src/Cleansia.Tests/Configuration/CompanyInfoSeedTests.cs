namespace Cleansia.Tests.Configuration;

/// <summary>
/// The company record <c>insert_seed_data.sql</c> writes is what every legal text, cleaner contract,
/// receipt and invoice prints through the company placeholders, so its contact values are pinned here,
/// each to its own column. Owner decision 2026-10-03: its e-mail is support@cleansia.cz, the one support
/// address customers and cleaners see.
/// </summary>
public class CompanyInfoSeedTests
{
    [Fact]
    public void The_Seeded_Company_Gives_The_Support_Address_As_Its_E_Mail()
    {
        Assert.Equal("'support@cleansia.cz'", SeededCompany()["Email"]);
    }

    /// <summary>The seed's one company row, column name to the SQL expression written for it.</summary>
    private static Dictionary<string, string> SeededCompany()
    {
        var seed = Seed();
        var start = seed.IndexOf("INSERT INTO public.\"CompanyInfo\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "The seed writes no company record.");
        Assert.Equal(-1, seed.IndexOf("INSERT INTO public.\"CompanyInfo\"", start + 1, StringComparison.Ordinal));

        // Comments are stripped: the statement annotates its values, and a value must be read off the SQL.
        var statement = string.Join('\n', seed[start..seed.IndexOf(';', start)]
            .Split('\n')
            .Select(line => line.Contains("--", StringComparison.Ordinal) ? line[..line.IndexOf("--", StringComparison.Ordinal)] : line));
        var columnsStart = statement.IndexOf('(') + 1;
        var columns = statement[columnsStart..statement.IndexOf(')', columnsStart)]
            .Split(',')
            .Select(column => column.Trim().Trim('"'))
            .ToList();
        var values = TopLevelItems(statement[(statement.IndexOf("VALUES", StringComparison.Ordinal) + "VALUES".Length)..].Trim()[1..^1]);

        Assert.Equal(columns.Count, values.Count);
        return columns.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);
    }

    // A value can be a sub-select with commas of its own, or a literal holding one.
    private static List<string> TopLevelItems(string list)
    {
        var items = new List<string>();
        var depth = 0;
        var quoted = false;
        var from = 0;
        for (var i = 0; i < list.Length; i++)
        {
            switch (list[i])
            {
                case '\'': quoted = !quoted; break;
                case '(' when !quoted: depth++; break;
                case ')' when !quoted: depth--; break;
                case ',' when !quoted && depth == 0:
                    items.Add(list[from..i].Trim());
                    from = i + 1;
                    break;
            }
        }

        items.Add(list[from..].Trim());
        return items;
    }

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
