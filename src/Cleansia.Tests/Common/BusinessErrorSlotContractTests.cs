using System.Text;
using System.Text.RegularExpressions;

namespace Cleansia.Tests.Common;

/// <summary>
/// consistency.md B5 — <c>new Error(nameof(command.Field), BusinessErrorMessage.X)</c>: the FIRST slot
/// is the offending field, the SECOND is the dot key. Swapping them is silent and total.
/// <c>CleansiaApiController.CreateProblemDetails</c> builds the <c>errors</c> bag as
/// <c>{ Error.Code : Error.Message }</c>, and every client resolves the first bag VALUE — web
/// <c>firstErrorKey</c>, Android <c>SafeApiCall.firstErrorKey</c>, iOS <c>ProblemDetails.firstErrorKey</c>.
/// A swapped pair therefore hands all three a human sentence to look up, they miss, and the user is
/// shown the generic "An error occurred" while the real translation sits unreached.
///
/// The i18n parity guards cannot see this: they check that every constant HAS a translation, which
/// stays true. Only the slot itself is checkable, and only at the source level — the swap is legal
/// C# (both slots are <c>string</c>) and no runtime reflection over the constants can observe which
/// argument a call site passed them to.
///
/// <para>The adjacent defect lives here too: a key written as a bare literal rather than as a
/// <c>BusinessErrorMessage</c> constant. The parity guards read <c>BusinessErrorMessage.cs</c>, so a key
/// that never becomes a constant is invisible to them by construction — the guard is not failing, it is
/// not looking — and every client falls through to <c>api.common.error_occurred</c>. Reflection cannot
/// see it either, for the same reason as above.</para>
/// </summary>
public class BusinessErrorSlotContractTests
{
    private const string ScannedProject = "Cleansia.Core.AppServices";

    /// <summary>The shape every client needs in the message slot to resolve a translation, and the
    /// same one Android's <c>SafeApiCall</c> uses to tell a business key from prose.</summary>
    private static readonly Regex DotKeyShape = new(@"^[a-z0-9_]+(\.[a-z0-9_]+)+$", RegexOptions.Compiled);

    private static readonly Regex NewErrorCall = new(@"\bnew\s+Error\s*\(", RegexOptions.Compiled);

    /// <summary>FluentValidation's message slot. <c>.WithMessage</c> is used in this project alone —
    /// no other project in the solution calls it — so scanning it here scans all of it.</summary>
    private static readonly Regex WithMessageCall = new(@"\.WithMessage\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// The Stripe webhook is the one caller that is not a person: Stripe reads the status code and
    /// retries, no locale is involved, and minting a <c>BusinessErrorMessage</c> for it would oblige
    /// all three apps to carry five translations nobody can ever see. Held as an exhaustive list
    /// rather than a path exclusion so a third prose literal in that file still fails.
    /// </summary>
    private static readonly (string File, string Literal)[] NonHumanCallerExemptions =
    {
        ("Features/Payments/HandlePaymentNotification.cs", "\"Invalid webhook signature\""),
        ("Features/Payments/HandlePaymentNotification.cs", "\"Order ID not found in webhook metadata\""),
    };

    private sealed record Construction(string RelativePath, int Line, string CodeSlot, string MessageSlot);

    private sealed record MessageArgument(string RelativePath, int Line, string Expression, string Call);

    [Fact]
    public void No_BusinessErrorMessage_Constant_Sits_In_The_Code_Slot()
    {
        var offenders = Scan()
            .Where(c => ReferencesConstantValue(c.CodeSlot))
            .Select(c => $"{c.RelativePath}:{c.Line} -> new Error({c.CodeSlot}, {c.MessageSlot})")
            .OrderBy(x => x)
            .ToList();

        Assert.True(offenders.Count == 0,
            "consistency.md B5: the first Error argument is nameof(the offending field), never the "
            + "BusinessErrorMessage constant. With the constant here it becomes the `errors` bag KEY, "
            + "every client reads the bag VALUE, and the real message is never resolved:\n  "
            + string.Join("\n  ", offenders));
    }

    [Fact]
    public void No_Prose_Sits_In_The_Message_Slot()
    {
        var exempt = NonHumanCallerExemptions
            .Select(e => (File: Normalize(e.File), e.Literal))
            .ToHashSet();

        var offenders = Scan()
            .Where(c => IsStringLiteral(c.MessageSlot) && !DotKeyShape.IsMatch(Unquote(c.MessageSlot)))
            .Where(c => !exempt.Contains((c.RelativePath, c.MessageSlot)))
            .Select(c => $"{c.RelativePath}:{c.Line} -> new Error({c.CodeSlot}, {c.MessageSlot})")
            .OrderBy(x => x)
            .ToList();

        Assert.True(offenders.Count == 0,
            "The second Error argument must be a BusinessErrorMessage constant (or an expression that "
            + "yields one). A prose literal here is what every client tries, and fails, to translate:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The key must come from <c>BusinessErrorMessage</c>, not from a literal that merely looks like one.
    /// A bare dot key still reaches the client and still misses every locale file, but it is invisible to
    /// the parity guards — they enumerate the constants, so a key that never becomes one is never checked.
    /// Scoped to dot-notation because that is the shape the clients treat as a lookup; prose is a separate
    /// defect, caught for the Error slot by <see cref="No_Prose_Sits_In_The_Message_Slot"/>.
    /// </summary>
    [Fact]
    public void No_Bare_Dot_Key_Literal_Reaches_A_Message_Slot()
    {
        var fromErrorSlot = Scan()
            .Where(c => IsBareDotKeyLiteral(c.MessageSlot))
            .Select(c => $"{c.RelativePath}:{c.Line} -> new Error({c.CodeSlot}, {c.MessageSlot})");

        var fromWithMessage = ScanMessageArguments()
            .Where(a => IsBareDotKeyLiteral(a.Expression))
            .Select(a => $"{a.RelativePath}:{a.Line} -> {a.Call}({a.Expression})");

        var offenders = fromErrorSlot.Concat(fromWithMessage).OrderBy(x => x).ToList();

        Assert.True(offenders.Count == 0,
            "A dot-notation key must be a BusinessErrorMessage constant. Written as a literal it still "
            + "ships to the client, but the i18n parity guards enumerate the constants — so nothing "
            + "checks it has a translation and every client falls through to api.common.error_occurred:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Anti-vacuity for the fact above, which after a fix has nothing left to match and would therefore
    /// pass on a scanner that reached no code at all. Pins three separate links: the WithMessage scan
    /// finds the bulk of its call sites, it resolves a known constant-carrying argument, and it reaches a
    /// real string LITERAL argument — the branch the classifier judges. The classifier itself is pinned
    /// last, against the exact literal this guard was written for.
    /// </summary>
    [Fact]
    public void BareDotKey_Scanner_Reaches_Literal_Arguments()
    {
        var arguments = ScanMessageArguments();

        Assert.True(arguments.Count > 800,
            $"Expected the {ScannedProject} scan to find the bulk of its message arguments but found "
            + $"{arguments.Count} — the scanner, not the codebase, is what regressed.");

        Assert.Contains(arguments, a =>
            a.RelativePath == Normalize("Features/Orders/CreatePaymentIntent.cs")
            && a.Expression == "BusinessErrorMessage.OrderPaymentAlreadyPaid");

        var literals = arguments.Where(a => IsStringLiteral(a.Expression)).ToList();
        Assert.Contains(literals, a =>
            a.RelativePath == Normalize("Features/FeatureFlags/CreateFeatureFlag.cs"));

        Assert.True(IsBareDotKeyLiteral("\"order.payment.already_paid\""));
        Assert.False(IsBareDotKeyLiteral("BusinessErrorMessage.OrderPaymentAlreadyPaid"));
        Assert.False(IsBareDotKeyLiteral("\"Scope must be 'global', 'country', or 'tenant'.\""));
    }

    [Fact]
    public void Every_NonHumanCaller_Exemption_Is_Still_Real()
    {
        var constructions = Scan();

        var stale = NonHumanCallerExemptions
            .Where(e => !constructions.Any(c =>
                c.RelativePath == Normalize(e.File) && c.MessageSlot == e.Literal))
            .Select(e => $"{e.File} -> {e.Literal}")
            .ToList();

        Assert.True(stale.Count == 0,
            "An exemption no longer matches any construction. Delete it — a stale entry silently widens "
            + "the gate for whatever prose lands on that line next:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>
    /// Anti-vacuity. A source scan that matches nothing passes every assertion above, so pin that the
    /// parser reaches a real, known call site and splits its slots the right way round.
    /// </summary>
    [Fact]
    public void Scanner_Parses_Real_Call_Sites()
    {
        var constructions = Scan();

        Assert.True(constructions.Count > 150,
            $"Expected the {ScannedProject} scan to find the bulk of its Error constructions but found "
            + $"{constructions.Count} — the scanner, not the codebase, is what regressed.");

        // Normalize, like every other path comparison in this file. RelativePath comes from
        // Path.GetRelativePath, so it carries the platform separator; the three assertions above
        // wrap their literals and this one did not, which made the anti-vacuity check itself
        // vacuous on Windows — it failed there, and passed on CI, which is Linux.
        var grantConsent = Assert.Single(constructions, c =>
            c.RelativePath == Normalize("Features/Gdpr/GrantConsent.cs"));

        Assert.Equal("nameof(Command.ConsentType)", grantConsent.CodeSlot);
        Assert.Equal("BusinessErrorMessage.ConsentAlreadyGranted", grantConsent.MessageSlot);
    }

    private static bool ReferencesConstantValue(string slot)
    {
        // nameof(BusinessErrorMessage.X) yields the MEMBER NAME ("EmployeeNotFound"), never the dot
        // key, so it names the failing thing exactly as a field name would and is not the swap.
        var withoutNameof = Regex.Replace(slot, @"\bnameof\s*\([^)]*\)", string.Empty);
        return withoutNameof.Contains("BusinessErrorMessage.");
    }

    private static bool IsBareDotKeyLiteral(string expression) =>
        IsStringLiteral(expression) && DotKeyShape.IsMatch(Unquote(expression));

    private static bool IsStringLiteral(string slot) =>
        slot.StartsWith('"') || slot.StartsWith("$\"") || slot.StartsWith("@\"")
        || slot.StartsWith("$@\"") || slot.StartsWith("@$\"");

    private static string Unquote(string literal) => literal.Trim('$', '@', '"');

    private static string Normalize(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar);

    private static DirectoryInfo SrcRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cleansia.Api.sln")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate src/ root (Cleansia.Api.sln).");
        return dir!;
    }

    private static IEnumerable<(string Relative, string Text)> SourceFiles()
    {
        var projectRoot = new DirectoryInfo(Path.Combine(SrcRoot().FullName, ScannedProject));
        Assert.True(projectRoot.Exists, $"Scanned project directory missing: {ScannedProject}");

        var files = projectRoot.EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        foreach (var file in files)
        {
            yield return (
                Path.GetRelativePath(projectRoot.FullName, file.FullName),
                BlankComments(File.ReadAllText(file.FullName)));
        }
    }

    private static IReadOnlyList<MessageArgument> ScanMessageArguments()
    {
        var arguments = new List<MessageArgument>();

        foreach (var (relative, text) in SourceFiles())
        {
            foreach (Match match in WithMessageCall.Matches(text))
            {
                var openParen = match.Index + match.Length - 1;
                var argumentList = ExtractBalanced(text, openParen);
                if (argumentList is null)
                    continue;

                var args = SplitTopLevel(argumentList);
                if (args.Count != 1)
                    continue;

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                arguments.Add(new MessageArgument(relative, line, args[0], "WithMessage"));
            }
        }

        return arguments;
    }

    private static IReadOnlyList<Construction> Scan()
    {
        var constructions = new List<Construction>();

        foreach (var (relative, text) in SourceFiles())
        {
            foreach (Match match in NewErrorCall.Matches(text))
            {
                var openParen = match.Index + match.Length - 1;
                var argumentList = ExtractBalanced(text, openParen);
                if (argumentList is null)
                    continue;

                var args = SplitTopLevel(argumentList);

                // A named-argument call (`new Error(message: …, code: …)`) would defeat positional
                // slot reading. Fail loudly rather than pass a construction this parser cannot judge.
                Assert.DoesNotContain(args, a => Regex.IsMatch(a, @"^\w+\s*:(?!:)"));

                if (args.Count != 2)
                    continue;

                var line = text.Take(match.Index).Count(c => c == '\n') + 1;
                constructions.Add(new Construction(relative, line, args[0], args[1]));
            }
        }

        return constructions;
    }

    /// <summary>Replaces comment spans with spaces, preserving offsets, so a commented-out or
    /// prose-carrying comment cannot be read as a call site. String literals are left intact.</summary>
    private static string BlankComments(string text)
    {
        var result = new StringBuilder(text);
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') result[i++] = ' ';
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                {
                    if (text[i] != '\n') result[i] = ' ';
                    i++;
                }
                for (var k = 0; k < 2 && i < text.Length; k++) result[i++] = ' ';
                continue;
            }

            if (c == '\'')
            {
                i++;
                while (i < text.Length && text[i] != '\'')
                    i += text[i] == '\\' ? 2 : 1;
                i++;
                continue;
            }

            if (c == '"')
            {
                var verbatim = i > 0 && (text[i - 1] == '@' || (i > 1 && text[i - 1] == '$' && text[i - 2] == '@'));
                i++;
                while (i < text.Length)
                {
                    if (verbatim)
                    {
                        if (text[i] == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"') { i += 2; continue; }
                            break;
                        }
                        i++;
                    }
                    else
                    {
                        if (text[i] == '\\') { i += 2; continue; }
                        if (text[i] == '"') break;
                        i++;
                    }
                }
                i++;
                continue;
            }

            i++;
        }

        return result.ToString();
    }

    private static string? ExtractBalanced(string text, int openParen)
    {
        var depth = 0;

        for (var i = openParen; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '"')
            {
                i = SkipLiteral(text, i);
                continue;
            }

            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return text[(openParen + 1)..i];
        }

        return null;
    }

    private static List<string> SplitTopLevel(string arguments)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        for (var i = 0; i < arguments.Length; i++)
        {
            var c = arguments[i];

            if (c == '"')
            {
                var end = SkipLiteral(arguments, i);
                current.Append(arguments[i..(end + 1)]);
                i = end;
                continue;
            }

            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;

            if (c == ',' && depth == 0)
            {
                args.Add(Collapse(current.ToString()));
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.ToString().Trim().Length > 0)
            args.Add(Collapse(current.ToString()));

        return args;
    }

    /// <summary>Index of the literal's closing quote, given <paramref name="start"/> is its opener.</summary>
    private static int SkipLiteral(string text, int start)
    {
        var verbatim = start > 0 && text[start - 1] == '@';

        for (var i = start + 1; i < text.Length; i++)
        {
            if (!verbatim && text[i] == '\\') { i++; continue; }
            if (text[i] != '"') continue;
            if (verbatim && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
            return i;
        }

        return text.Length - 1;
    }

    private static string Collapse(string argument) =>
        Regex.Replace(argument.Trim(), @"\s+", " ");
}
