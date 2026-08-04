using System.Text.RegularExpressions;
using Cleansia.Infra.Services.Pdf.Models;

namespace Cleansia.Tests.Configuration;

/// <summary>
/// The seed is where "which jurisdictions have we actually had reviewed" is answered, and the answer is
/// one. Nine of the rows this file used to carry stated something about a foreign country's law that no
/// lawyer had seen, and the Czech row stated Czech law in English — none of which any assertion on the
/// field model could notice. This pin fails the moment a second jurisdiction's notice is seeded without
/// someone deciding, in writing, what stands behind it.
/// </summary>
public class CountryInvoiceLegalNoticeSeedTests
{
    [Fact]
    public void Exactly_One_Jurisdiction_Seeds_A_Legal_Notice()
    {
        Assert.Single(Regex.Matches(Statements(), "\"LegalDisclaimerTemplate\""));
    }

    [Fact]
    public void That_Jurisdiction_Is_Czechia_And_Its_Notice_Is_Czech()
    {
        var statement = LegalNoticeStatement();

        Assert.Contains("'CZE'", statement);
        Assert.Contains("\"LegalDisclaimerLanguageCode\" = 'cs'", statement);
        Assert.Contains("Dovolujeme si Vás upozornit", statement);
    }

    // BusinessSupplied (1), not CounselReviewed (2): the wording is verbatim off an invoice the owner
    // issues, which is real provenance and is not a lawyer's opinion. Seeding 2 would claim a review
    // that has not happened.
    [Fact]
    public void The_Czech_Notice_Is_Recorded_As_Business_Supplied_Rather_Than_Counsel_Reviewed()
    {
        Assert.Contains("\"LegalDisclaimerReviewStatus\" = 1", LegalNoticeStatement());
    }

    [Fact]
    public void The_Insert_Leaves_Every_Country_Unreviewed_So_The_Fallback_Is_The_Default()
    {
        var insertColumns = Regex.Match(
            Statements(),
            "INSERT INTO public\\.\"CountryInvoiceConfigs\" \\(([^)]*)\\)").Groups[1].Value;

        Assert.DoesNotContain("LegalDisclaimer", insertColumns);
    }

    // The reviewed notice and the fallback must never be the same string: if they were, nobody reading
    // the document could tell a reviewed jurisdiction from one nobody has looked at.
    [Fact]
    public void The_Reviewed_Notice_Is_Not_The_Generic_Fallback()
    {
        Assert.DoesNotContain(InvoiceLabels.UnreviewedJurisdictionNotice, Statements());
    }

    private static string LegalNoticeStatement()
    {
        var sql = Statements();
        var marker = sql.IndexOf("\"LegalDisclaimerTemplate\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, "The seed script carries no legal notice at all.");

        return sql[sql.LastIndexOf("UPDATE", marker, StringComparison.Ordinal)..sql.IndexOf(';', marker)];
    }

    // Comments are stripped: this file explains its own decisions at length, and a rule counted over the
    // prose would be satisfied or broken by editing a comment.
    private static string Statements() =>
        string.Join('\n', Seed().Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));

    // Mirrors StartupSeedScriptSyncTests — walk up to the *.sln, then across to the canonical script.
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
