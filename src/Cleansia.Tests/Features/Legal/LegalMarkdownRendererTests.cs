using Cleansia.Core.AppServices.Features.Legal;

namespace Cleansia.Tests.Features.Legal;

/// <summary>
/// The seed markdown reaches a page as HTML the server rendered: headings and paragraphs come out as
/// elements, a market placeholder is filled with the market's value before parsing (so it is escaped
/// like any text), an unknown placeholder is left visible rather than silently blanked, and raw HTML in
/// the source never passes through — a seed file cannot carry markup into a page.
/// </summary>
public sealed class LegalMarkdownRendererTests
{
    private static readonly IReadOnlyDictionary<string, string> Czk = new Dictionary<string, string> { ["currency"] = "CZK" };

    [Fact]
    public void Headings_And_Paragraphs_Render_As_Elements()
    {
        var html = LegalMarkdownRenderer.Render("## Ordering\n\nPrices are final.\n");

        Assert.Contains("<h2>Ordering</h2>", html);
        Assert.Contains("<p>Prices are final.</p>", html);
    }

    [Fact]
    public void A_Known_Placeholder_Is_Filled_With_The_Markets_Value()
    {
        var html = LegalMarkdownRenderer.Render("Prices are displayed in {{currency}} and are final.", Czk);

        Assert.Contains("Prices are displayed in CZK and are final.", html);
        Assert.DoesNotContain("{{", html);
    }

    [Fact]
    public void Whitespace_Inside_The_Braces_Still_Names_The_Placeholder()
    {
        var html = LegalMarkdownRenderer.Render("in {{ currency }} only", Czk);

        Assert.Contains("in CZK only", html);
    }

    [Fact]
    public void An_Unknown_Placeholder_Stays_Visible()
    {
        var html = LegalMarkdownRenderer.Render("Insured up to {{insurance}}.", Czk);

        Assert.Contains("Insured up to {{insurance}}.", html);
    }

    [Fact]
    public void Without_Placeholder_Values_The_Text_Renders_Verbatim()
    {
        var html = LegalMarkdownRenderer.Render("Prices in {{currency}}.");

        Assert.Contains("Prices in {{currency}}.", html);
    }

    [Fact]
    public void Raw_Html_In_The_Source_Is_Escaped_Not_Rendered()
    {
        var html = LegalMarkdownRenderer.Render("Hello <script>alert(1)</script> <b>there</b>");

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void A_Placeholder_Value_Is_Escaped_Like_Any_Text()
    {
        var html = LegalMarkdownRenderer.Render("in {{currency}}", new Dictionary<string, string> { ["currency"] = "<img src=x>" });

        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;img src=x&gt;", html);
    }

    [Fact]
    public void The_Placeholders_A_Text_Carries_Are_Listed_By_Name()
    {
        var names = LegalMarkdownRenderer.PlaceholdersIn("{{currency}} and {{ currency }} and {{insurance}} but not {single}");

        Assert.Equal(new[] { "currency", "insurance" }, names.OrderBy(n => n));
    }
}
