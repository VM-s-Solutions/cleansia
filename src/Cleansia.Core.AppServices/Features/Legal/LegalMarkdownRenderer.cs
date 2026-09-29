using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Markdig;
using Markdig.Syntax;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// Markdown to HTML for the legal texts. Raw HTML in the source is rendered as text, not passed
/// through, so a seed file can never carry markup into a page; the <c>{{name}}</c> placeholders are
/// substituted BEFORE parsing so a market's figure is escaped like any other text.
/// </summary>
public static partial class LegalMarkdownRenderer
{
    public const string CurrencyPlaceholder = "currency";
    public const string CompanyLegalNamePlaceholder = "companyLegalName";
    public const string CompanyRegistrationNumberPlaceholder = "companyRegistrationNumber";
    public const string CompanyVatNumberPlaceholder = "companyVatNumber";
    public const string CompanySeatPlaceholder = "companySeat";
    public const string CompanyEmailPlaceholder = "companyEmail";
    public const string CompanyPhonePlaceholder = "companyPhone";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();

    /// <summary>
    /// The market's currency and the identity of the company that operates it, as its receipts print it.
    /// A value the company record does not hold is left out, so its placeholder stays visible on the page.
    /// </summary>
    public static Dictionary<string, string> MarketPlaceholders(CountryConfiguration? market, CompanyInfo? company)
    {
        var placeholders = new Dictionary<string, string>(StringComparer.Ordinal);
        if (market is not null)
        {
            placeholders[CurrencyPlaceholder] = market.DefaultCurrencyCode;
        }

        if (company is null)
        {
            return placeholders;
        }

        var identity = new (string Name, string? Value)[]
        {
            (CompanyLegalNamePlaceholder, company.LegalName),
            (CompanyRegistrationNumberPlaceholder, company.RegistrationNumber),
            (CompanyVatNumberPlaceholder, company.VatNumber),
            (CompanySeatPlaceholder, company.GetFullAddress()),
            (CompanyEmailPlaceholder, company.Email),
            (CompanyPhonePlaceholder, company.Phone),
        };
        foreach (var (name, value) in identity)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                placeholders[name] = value;
            }
        }

        return placeholders;
    }

    public static string Render(string markdown, IReadOnlyDictionary<string, string>? placeholders = null) =>
        Markdown.ToHtml(Fill(markdown, placeholders), Pipeline);

    /// <summary>
    /// The text for a document that is not a page — the PDF copy of a contract: one entry per top-level
    /// block in reading order, its inline markup dropped, a heading flagged.
    /// </summary>
    public static IReadOnlyList<(string Text, bool IsHeading)> RenderPlainBlocks(
        string markdown, IReadOnlyDictionary<string, string>? placeholders = null)
    {
        var source = Fill(markdown, placeholders);
        return Markdown.Parse(source, Pipeline)
            .Select(block => (
                Text: Markdown.ToPlainText(source.Substring(block.Span.Start, block.Span.Length), Pipeline).Trim(),
                IsHeading: block is HeadingBlock))
            .Where(block => block.Text.Length > 0)
            .ToList();
    }

    private static string Fill(string markdown, IReadOnlyDictionary<string, string>? placeholders) =>
        placeholders is null || placeholders.Count == 0
            ? markdown
            : Placeholder().Replace(markdown, match =>
                placeholders.TryGetValue(match.Groups["name"].Value, out var value) ? value : match.Value);

    /// <summary>The placeholder names a text carries, for the seed tests and the admin preview.</summary>
    public static IReadOnlySet<string> PlaceholdersIn(string markdown) =>
        Placeholder().Matches(markdown).Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z0-9_]+)\s*\}\}")]
    private static partial Regex Placeholder();
}
