using System.Text.RegularExpressions;
using Markdig;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// Markdown to HTML for the legal texts. Raw HTML in the source is rendered as text, not passed
/// through, so a seed file can never carry markup into a page; the <c>{{name}}</c> placeholders are
/// substituted BEFORE parsing so a market's figure is escaped like any other text.
/// </summary>
public static partial class LegalMarkdownRenderer
{
    public const string CurrencyPlaceholder = "currency";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();

    public static string Render(string markdown, IReadOnlyDictionary<string, string>? placeholders = null)
    {
        var source = placeholders is null || placeholders.Count == 0
            ? markdown
            : Placeholder().Replace(markdown, match =>
                placeholders.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value);

        return Markdown.ToHtml(source, Pipeline);
    }

    /// <summary>The placeholder names a text carries, for the seed tests and the admin preview.</summary>
    public static IReadOnlySet<string> PlaceholdersIn(string markdown) =>
        Placeholder().Matches(markdown).Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z0-9_]+)\s*\}\}")]
    private static partial Regex Placeholder();
}
