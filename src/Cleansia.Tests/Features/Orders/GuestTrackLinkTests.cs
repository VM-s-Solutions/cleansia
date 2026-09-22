using System.Text.RegularExpressions;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Every order e-mail carries a "view your booking" button, and for a GUEST that button is only a
/// link if it carries a credential — there is no account to sign in to. <c>BuildOrderStatusLink</c>
/// appends one only when the caller supplies it, so a call site that omits the argument ships a
/// button that lands the customer on the "the link is in your confirmation e-mail" panel.
///
/// <para>That is not a hypothetical: it is what the first cut of the re-key shipped, on all four
/// status e-mails at once, because the single-channel design it assumed had no second minting site.
/// A behavioural test per handler would not have caught it — each would have been written green
/// against the site it already knew about. This walks the tree instead, so the FIFTH site is caught
/// the day it is written.</para>
/// </summary>
public class GuestTrackLinkTests
{
    private const string SendMarker = "SendOrderStatusUpdateEmailAsync(";
    private const string TokenArgument = "guestAccessToken";

    private static readonly string[] ExcludedProjectPrefixes =
    [
        "Cleansia.Tests",
        "Cleansia.IntegrationTests",
        "Cleansia.HostTests",
        "Cleansia.TestUtilities",
    ];

    /// <summary>The declaring sites: the interface and the implementation, which take the argument
    /// rather than passing one.</summary>
    private static readonly string[] DeclaringFiles =
    [
        Path.Combine("Cleansia.Core.AppServices", "Services", "Interfaces", "IEmailService.cs"),
        Path.Combine("Cleansia.Core.AppServices", "Services", "EmailService.cs"),
    ];

    [Fact]
    public void Every_Status_Email_Passes_A_Guest_Access_Token()
    {
        var srcRoot = RequireSolutionDirectory();
        var callSites = ProductionSourceFiles(srcRoot)
            .Select(file => (Path: Path.GetRelativePath(srcRoot, file), Text: File.ReadAllText(file)))
            .Where(f => f.Text.Contains(SendMarker, StringComparison.Ordinal))
            .Where(f => !DeclaringFiles.Any(d => f.Path.EndsWith(d, StringComparison.Ordinal)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();

        var offenders = callSites
            .Where(f => CallsWithoutTheToken(f.Text) > 0)
            .Select(f => f.Path)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"These sites send an order status e-mail without a {TokenArgument}, so a guest who opens it "
            + "gets a button that cannot open their booking. Mint one with "
            + "GuestOrderAccessTokenIssuer.IssueForGuest(order) — it returns null for an account "
            + "booking, which is the right answer there. Offenders:\n  " + string.Join("\n  ", offenders));

        // Anti-vacuity: a rename that made the marker match nothing would otherwise pass silently.
        Assert.True(callSites.Count >= 4,
            $"Only {callSites.Count} call site(s) found — the marker no longer matches the code it guards.");
    }

    /// <summary>
    /// Counts invocations whose argument list does not name the token. The list is read to its
    /// matching close paren rather than to end-of-line, because three of the four sites wrap.
    /// </summary>
    private static int CallsWithoutTheToken(string source)
    {
        var missing = 0;
        foreach (Match match in Regex.Matches(source, Regex.Escape(SendMarker)))
        {
            var start = match.Index + SendMarker.Length;
            var depth = 1;
            var i = start;
            while (i < source.Length && depth > 0)
            {
                if (source[i] == '(') depth++;
                else if (source[i] == ')') depth--;
                i++;
            }

            if (!source[start..(i - 1)].Contains(TokenArgument, StringComparison.Ordinal))
            {
                missing++;
            }
        }

        return missing;
    }

    private static IEnumerable<string> ProductionSourceFiles(string srcRoot)
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
