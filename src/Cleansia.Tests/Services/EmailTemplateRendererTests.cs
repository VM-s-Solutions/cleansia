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
