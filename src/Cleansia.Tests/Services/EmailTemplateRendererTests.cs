using System.Text.RegularExpressions;
using Cleansia.Core.AppServices.Services;
using Xunit;

namespace Cleansia.Tests.Services;

/// <summary>
/// Guards the seam that makes <c>email-templates/</c> the runtime source rather
/// than documentation. If the csproj link that embeds the folder is ever
/// dropped, these fail at build time instead of in a customer's inbox.
/// </summary>
public class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer renderer = new();

    [Theory]
    [InlineData("promo-code.html")]
    [InlineData("email-confirmation.html")]
    [InlineData("password-reset.html")]
    [InlineData("order-receipt.html")]
    [InlineData("order-status-update.html")]
    [InlineData("close-period-notification.html")]
    [InlineData("closure-period-reminder.html")]
    [InlineData("company-wind-down-customer.html")]
    [InlineData("company-wind-down-cleaner.html")]
    [InlineData("admin-notification.html")]
    public void Every_template_in_the_repository_folder_is_embedded(string templateName)
    {
        var html = renderer.Render(templateName, new Dictionary<string, string?>());

        Assert.False(string.IsNullOrWhiteSpace(html));
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Placeholders_are_replaced_with_their_values()
    {
        var html = renderer.Render("promo-code.html", new Dictionary<string, string?>
        {
            ["PromoCode"] = "CLEAN-7Q2M",
            ["Subject"] = "Your Cleansia discount code",
            ["DiscountText"] = "Discount on your first order",
        });

        Assert.Contains("CLEAN-7Q2M", html, StringComparison.Ordinal);
        Assert.Contains("Your Cleansia discount code", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{PromoCode}}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_is_html_encoded_so_markup_in_a_name_is_shown_not_rendered()
    {
        var html = renderer.Render("email-confirmation.html", new Dictionary<string, string?>
        {
            ["UserName"] = "<b>Ann & Co</b>",
            ["Greeting"] = "Dobrý den — Привіт",
        });

        Assert.Contains("&lt;b&gt;Ann &amp; Co&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>Ann", html, StringComparison.Ordinal);
        // Only the four characters that can break out are touched: the copy keeps its letters.
        Assert.Contains("Dobrý den — Привіт", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_holding_another_keys_placeholder_prints_as_typed_and_is_never_filled()
    {
        // UserName is offered before SupportEmail, as EmailService offers them. Replacing key by key
        // filled the name's braces with the support address when SupportEmail's turn came.
        var html = renderer.Render("email-confirmation.html", new Dictionary<string, string?>
        {
            ["UserName"] = "{{SupportEmail}}",
            ["SupportEmail"] = "support@cleansia.cz",
        });

        Assert.Contains("<strong>{{SupportEmail}}</strong>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"mailto:support@cleansia.cz\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_holding_braces_no_key_names_prints_as_typed_rather_than_being_stripped()
    {
        var html = renderer.Render("email-confirmation.html", new Dictionary<string, string?>
        {
            ["UserName"] = "Ann {{Nickname}}",
        });

        Assert.Contains("<strong>Ann {{Nickname}}</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_quote_in_a_value_cannot_leave_the_attribute_it_is_written_into()
    {
        var html = renderer.Render("order-status-update.html", new Dictionary<string, string?>
        {
            ["OrderStatusLink"] = "https://pay.test/x\" onclick=\"steal()",
        });

        Assert.Contains("href=\"https://pay.test/x&quot; onclick=&quot;steal()\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick=\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The renderer encodes the four characters that break element text and double-quoted attributes,
    /// not the apostrophe. A placeholder in a single-quoted or unquoted attribute, or inside a style or
    /// script block, would be a context that encoding does not cover, so no template may put one there.
    /// </summary>
    [Fact]
    public void Every_placeholder_sits_in_element_text_or_a_double_quoted_attribute()
    {
        const string prefix = "Cleansia.Core.AppServices.EmailTemplates.";
        var assembly = typeof(EmailTemplateRenderer).Assembly;
        var templates = assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(templates);

        foreach (var name in templates)
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var html = reader.ReadToEnd();

            foreach (Match block in Regex.Matches(html, "<(style|script)[^>]*>(.*?)</\\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                Assert.False(block.Groups[2].Value.Contains("{{", StringComparison.Ordinal), $"{name}: placeholder inside a <{block.Groups[1].Value}> block");
            }

            foreach (Match tag in Regex.Matches(html, "<[^<>]*\\{\\{[^<>]*>"))
            {
                var outsideDoubleQuotes = Regex.Replace(tag.Value, "\"[^\"]*\"", "\"\"");
                Assert.False(outsideDoubleQuotes.Contains("{{", StringComparison.Ordinal), $"{name}: placeholder outside a double-quoted attribute in {tag.Value}");
            }
        }
    }

    [Fact]
    public void A_key_that_was_not_supplied_renders_empty_rather_than_leaving_braces()
    {
        // A customer must never receive a page of handlebars because one optional
        // value was null — an absent expiry date is the real case here.
        var html = renderer.Render("promo-code.html", new Dictionary<string, string?>
        {
            ["PromoCode"] = "CLEAN-7Q2M",
            ["ExpiryNotice"] = null,
        });

        Assert.DoesNotContain("{{ExpiryNotice}}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{PromoCode}}", html, StringComparison.Ordinal);
    }

    [Fact]
    public void A_key_that_was_never_offered_at_all_renders_empty_too()
    {
        // The null case above is the caller saying "this one is empty". THIS is the
        // caller never mentioning the key — a translation row missing for one
        // locale. It used to leave "{{Greeting}}" in the customer's inbox.
        var html = renderer.Render("promo-code.html", new Dictionary<string, string?>
        {
            ["PromoCode"] = "CLEAN-7Q2M",
        });

        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_template_fails_loudly_and_names_what_is_available()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => renderer.Render("does-not-exist.html", new Dictionary<string, string?>()));

        Assert.Contains("does-not-exist.html", ex.Message, StringComparison.Ordinal);
        Assert.Contains("promo-code.html", ex.Message, StringComparison.Ordinal);
    }
}
