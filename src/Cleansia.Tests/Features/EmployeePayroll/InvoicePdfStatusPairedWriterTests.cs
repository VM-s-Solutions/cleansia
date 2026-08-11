namespace Cleansia.Tests.Features.EmployeePayroll;

/// <summary>
/// <c>EmployeeInvoice</c>'s PDF status is a two-sided fact: a site that records a successful render
/// (<c>SetPdfBlobUrl</c>) must also record a failed one (<c>SetPdfGenerationError</c>), or the row
/// reads healthy while its stored document is stale and the only signal is a log line. The two render
/// sites are duplicates rather than one seam, so this is what stops them diverging again — and what
/// makes a THIRD render site declare which half it forgot.
/// </summary>
public class InvoicePdfStatusPairedWriterTests
{
    private const string SuccessWriter = "SetPdfBlobUrl(";
    private const string FailureWriter = "SetPdfGenerationError(";

    private static readonly string[] ExcludedProjectPrefixes =
    [
        "Cleansia.Tests",
        "Cleansia.IntegrationTests",
        "Cleansia.HostTests",
        "Cleansia.TestUtilities",
    ];

    [Fact]
    public void Every_Site_That_Records_A_Rendered_Pdf_Also_Records_A_Failed_One()
    {
        var srcRoot = RequireSolutionDirectory();

        var successSites = ProductionSources(srcRoot)
            .Where(file => File.ReadAllText(file).Contains(SuccessWriter, StringComparison.Ordinal))
            // The entity declares both writers; it is not a render site.
            .Where(file => !file.EndsWith(Path.Combine("EmployeePayroll", "EmployeeInvoice.cs"), StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(srcRoot, file))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var oneSided = successSites
            .Where(file => !File.ReadAllText(Path.Combine(srcRoot, file)).Contains(FailureWriter, StringComparison.Ordinal))
            .ToList();

        Assert.True(oneSided.Count == 0,
            "A render site stores a PDF URL but never records a failed render, so a failed regenerate " +
            "leaves the row reading healthy. Call invoice.SetPdfGenerationError(...) on the failure " +
            "path and commit it — the UnitOfWork pipeline commits successful commands only. Offenders:\n  " +
            string.Join("\n  ", oneSided));

        // Anti-vacuity: an empty scan is not a pass. Both known render sites must be in the corpus.
        Assert.Contains(successSites, f => f.EndsWith("RegenerateInvoicePdf.cs", StringComparison.Ordinal));
        Assert.Contains(successSites, f => f.EndsWith("PayPeriodBackgroundService.cs", StringComparison.Ordinal));
    }

    private static IEnumerable<string> ProductionSources(string srcRoot)
    {
        return Directory.EnumerateDirectories(srcRoot, "Cleansia.*", SearchOption.TopDirectoryOnly)
            .Where(dir =>
            {
                var name = Path.GetFileName(dir);
                // Cleansia.App is the Angular monorepo (node_modules) — no C# and slow to walk.
                return name != "Cleansia.App"
                    && !ExcludedProjectPrefixes.Any(p => name.Equals(p, StringComparison.Ordinal));
            })
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static string RequireSolutionDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }

        Assert.Fail("Could not locate the solution directory from the test base directory.");
        return string.Empty;
    }
}
