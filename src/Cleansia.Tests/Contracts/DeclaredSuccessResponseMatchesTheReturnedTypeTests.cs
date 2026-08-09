using System.Text.RegularExpressions;

namespace Cleansia.Tests.Contracts;

/// <summary>
/// An action's declared 200 body is the body it actually returns.
///
/// <para><b>Why this is worth a test.</b> `[ProducesResponseType(typeof(X), Status200OK)]` is the only
/// thing the OpenAPI document knows about a success body, and NSwag generates the TypeScript client
/// from that document — so a copy-pasted attribute does not fail anything, it silently types the
/// generated client's result as some other feature's response. Nothing in the compiler, the runtime or
/// any existing test compares the attribute against the `HandleResult&lt;T&gt;` on the next line: one is an
/// attribute argument, the other a generic argument, and they never meet.</para>
///
/// <para>Found by a frontend lane on 2026-08-09 while deriving the error-contract surface from the
/// controllers. Reading `ProducesResponseType` as a dispatch pulled <c>CreateOrder</c> onto the
/// <b>partner</b> error surface — because `PUT /api/Employee/UpdateEmployee` declared
/// <c>CreateOrder.Response</c> while returning <c>UpdateEmployee.Response</c> — and injected fifteen
/// false keys. The wrong attribute was the finding; the false keys were how it surfaced.</para>
///
/// <para><b>Scope, stated honestly.</b> This compares only actions whose returned type is written down
/// at the return site — <c>HandleResult&lt;T&gt;</c> or <c>HandleTokenIssuingResult&lt;T&gt;</c>. An action
/// returning <c>Ok(result)</c> carries no type there, so there is nothing to compare; it is counted,
/// not asserted, and the count is reported by the anti-vacuity fact below so the blind half stays
/// visible rather than being mistaken for coverage.</para>
///
/// <para><b>The download shape is a second rule, not an exception.</b> Nine actions unwrap on the
/// failure branch (<c>if (!result.IsSuccess) return HandleResult&lt;DownloadX.Response&gt;(result);</c>) and
/// then <c>return File(...)</c>. Reading the first as the success body is what the naive comparison
/// does, and it is wrong: the 200 body of a download really is a file. So an action containing
/// <c>return File(</c> is asserted to declare <c>FileContentResult</c> — which is the same claim from
/// the other side, and covers the nine rather than excusing them.</para>
/// </summary>
public class DeclaredSuccessResponseMatchesTheReturnedTypeTests
{
    private static readonly string[] HostProjects =
    {
        "Cleansia.Web.Admin",
        "Cleansia.Web.Partner",
        "Cleansia.Web.Customer",
        "Cleansia.Web.Mobile.Partner",
        "Cleansia.Web.Mobile.Customer",
    };

    /// <summary>`[ProducesResponseType(typeof(Foo.Bar), StatusCodes.Status200OK)]` — the 200 arm only.</summary>
    private static readonly Regex DeclaredSuccessBody = new(
        @"\[ProducesResponseType\(\s*typeof\(([^)]+)\)\s*,\s*StatusCodes\.Status200OK\s*\)\]",
        RegexOptions.Compiled);

    /// <summary>`HandleResult&lt;Foo.Bar&gt;(` / `HandleTokenIssuingResult&lt;Foo.Bar&gt;(` — the returned type.</summary>
    private static readonly Regex ReturnedBody = new(
        @"Handle(?:TokenIssuing)?Result<([^>]+)>\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex ActionStart = new(
        @"^\s*\[Http(?:Get|Post|Put|Patch|Delete)\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private sealed record Action(string Host, string File, string Text, int Line);

    private sealed record Comparison(string Host, string Controller, int Line, string Declared, string Returned)
    {
        /// <summary>`CompanyInfoDetailDto?` and `CompanyInfoDetailDto` are the same wire body.</summary>
        private static string Normalize(string type) => type.TrimEnd('?', ' ');

        public bool Agrees => Normalize(Declared) == Normalize(Returned);

        public override string ToString() =>
            $"{Host}/{Controller}:{Line} declares {Declared} but returns {Returned}";
    }

    // ── The fact ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void No_Action_Declares_A_Success_Body_It_Does_Not_Return()
    {
        var mismatches = Compare()
            .Where(c => !c.Agrees)
            .Select(c => c.ToString())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(mismatches.Count == 0,
            "These actions document one 200 body and return another. The OpenAPI document — and so "
            + "every generated client — carries the declared one:\n  "
            + string.Join("\n  ", mismatches));
    }

    [Fact]
    public void An_Action_That_Returns_A_File_Declares_A_File()
    {
        var mismatches = AllActions()
            .Where(a => a.Text.Contains("return File(", StringComparison.Ordinal))
            .Select(a => new
            {
                Action = a,
                Declared = DeclaredSuccessBody.Match(a.Text) is { Success: true } m ? m.Groups[1].Value.Trim() : null,
            })
            .Where(x => x.Declared != "FileContentResult")
            .Select(x => $"{x.Action.Host}/{Path.GetFileNameWithoutExtension(x.Action.File)}:{x.Action.Line} "
                         + $"returns a file but declares {x.Declared ?? "no 200 body"}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(mismatches.Count == 0, string.Join("\n  ", mismatches));
    }

    // ── Anti-vacuity: the scan reaches real actions, and its blind half is named ────────────────

    [Fact]
    public void The_Scan_Reaches_Every_Host_And_Reports_What_It_Cannot_Compare()
    {
        var actions = AllActions();
        Assert.True(actions.Count > 200, $"only {actions.Count} actions found across five hosts");

        foreach (var host in HostProjects)
        {
            Assert.True(actions.Any(a => a.Host == host), $"no actions found in {host}");
        }

        var comparisons = Compare();

        // A regex pair that matched nothing would pass the fact above in perfect silence.
        Assert.True(comparisons.Count > 100,
            $"only {comparisons.Count} of {actions.Count} actions were comparable — the scan has "
            + "probably stopped matching one of the two idioms");

        // Both regexes must be earning their keep independently.
        Assert.Contains(actions, a => DeclaredSuccessBody.IsMatch(a.Text));
        Assert.Contains(actions, a => ReturnedBody.IsMatch(a.Text));
    }

    // ── Reading ─────────────────────────────────────────────────────────────────────────────────

    private static List<Comparison> Compare()
    {
        var comparisons = new List<Comparison>();

        foreach (var action in AllActions())
        {
            // A download unwraps its FAILURE through HandleResult<T> and then returns File(...), so the
            // HandleResult type is not the success body. An_Action_That_Returns_A_File_Declares_A_File
            // makes the claim for these from the other side.
            if (action.Text.Contains("return File(", StringComparison.Ordinal))
                continue;

            var declared = DeclaredSuccessBody.Match(action.Text);
            var returned = ReturnedBody.Match(action.Text);

            if (!declared.Success || !returned.Success)
                continue;

            comparisons.Add(new Comparison(
                action.Host,
                Path.GetFileNameWithoutExtension(action.File),
                action.Line,
                declared.Groups[1].Value.Trim(),
                returned.Groups[1].Value.Trim()));
        }

        return comparisons;
    }

    /// <summary>
    /// One entry per action, sliced on the `[HttpVerb]` attribute that opens it — the attributes above a
    /// verb belong to the action below it, so slicing anywhere else would read one action's attribute
    /// against its neighbour's return.
    /// </summary>
    private static List<Action> AllActions()
    {
        var solutionDirectory = FindSolutionDirectory(AppContext.BaseDirectory);
        Assert.NotNull(solutionDirectory);

        var actions = new List<Action>();

        foreach (var host in HostProjects)
        {
            var controllers = Path.Combine(solutionDirectory!, host, "Controllers");
            Assert.True(Directory.Exists(controllers), $"no Controllers directory for {host} at {controllers}");

            foreach (var file in Directory.EnumerateFiles(controllers, "*.cs", SearchOption.AllDirectories))
            {
                var source = File.ReadAllText(file);
                var starts = ActionStart.Matches(source).Select(m => m.Index).ToList();

                for (var i = 0; i < starts.Count; i++)
                {
                    var from = starts[i];
                    var to = i + 1 < starts.Count ? starts[i + 1] : source.Length;
                    var line = source.Take(from).Count(c => c == '\n') + 1;
                    actions.Add(new Action(host, file, source[from..to], line));
                }
            }
        }

        return actions;
    }

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
