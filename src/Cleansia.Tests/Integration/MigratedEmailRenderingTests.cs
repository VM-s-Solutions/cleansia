using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// What each of the six migrated e-mails actually puts on the wire (T-0677).
///
/// <para>Before this ticket the six went out as SendGrid dynamic templates: the
/// repository held HTML nobody read, and the copy that reached a customer was
/// whatever had been pasted into a hosted template against an id in config. The
/// two could drift silently and no test could see it, because the body was never
/// in the request — only a template id and a bag of merge values.</para>
///
/// <para>Now the body IS in the request, so it can be asserted on. These are the
/// tests that were impossible to write before, and they are the reason the
/// migration is worth doing: a template edited in the repository is the template
/// that gets sent, and if a placeholder stops resolving, this suite says so
/// rather than a customer.</para>
/// </summary>
public class MigratedEmailRenderingTests
{
    private const string Recipient = "customer@example.com";

    [Fact]
    public async Task Confirmation_carries_the_code_and_the_name()
    {
        var (service, wire) = Build(new Dictionary<string, string>
        {
            ["Subject"] = "Confirm Your Email",
            ["Greeting"] = "Hello",
            ["IntroText"] = "One step left.",
        });

        await service.SendEmailConfirmationAsync(Recipient, "Jana", "483920", "cs", CancellationToken.None);

        Assert.Contains("483920", wire.Html, StringComparison.Ordinal);
        Assert.Contains("Jana", wire.Html, StringComparison.Ordinal);
        Assert.Contains("One step left.", wire.Html, StringComparison.Ordinal);
        // The subject carries the code so it is readable from an inbox list.
        Assert.Equal("Confirm Your Email - [483920]", wire.Subject);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Password_reset_carries_the_link_unescaped_by_the_renderer()
    {
        var (service, wire) = Build(new Dictionary<string, string>
        {
            ["Subject"] = "Reset Your Password",
            ["ButtonText"] = "Choose a new password",
        });

        await service.SendResetPasswordEmailAsync(Recipient, "Jana", "A1B2C3", "en", CancellationToken.None);

        // The reset URL is built with escaped query values; the renderer is a literal
        // substitution and must not touch them again.
        Assert.Contains("customer%40example.com", wire.Html, StringComparison.Ordinal);
        Assert.Contains("code=A1B2C3", wire.Html, StringComparison.Ordinal);
        Assert.Contains("Choose a new password", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Order_receipt_carries_the_order_and_keeps_its_pdf()
    {
        var (service, wire) = Build(new Dictionary<string, string> { ["Subject"] = "Your Order Receipt" });
        var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var order = NewOrder();

        await service.SendOrderReceiptEmailAsync(
            Recipient, order, pdf, "receipt-42.pdf", "en", CancellationToken.None);

        Assert.Contains(order.DisplayOrderNumber, wire.Html, StringComparison.Ordinal);
        Assert.Contains("Jana Novakova", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);

        // AC3 — the attachment survived the move off the templated sender.
        Assert.Equal("receipt-42.pdf", wire.AttachmentName);
        Assert.Equal(Convert.ToBase64String(pdf), wire.AttachmentContent);
    }

    [Fact]
    public async Task Order_status_update_renders_the_status_class_the_stylesheet_keys_on()
    {
        var (service, wire) = Build(new Dictionary<string, string>
        {
            ["Subject"] = "Order update",
            ["StatusMessage_Completed"] = "Your cleaning has been completed.",
        });

        await service.SendOrderStatusUpdateEmailAsync(
            Recipient, NewOrder(), "completed", "en", CancellationToken.None);


        Assert.Contains("Your cleaning has been completed.", wire.Html, StringComparison.Ordinal);
        Assert.Contains("COMPLETED", wire.Html, StringComparison.Ordinal);
        // StatusClass is substituted into a class attribute, not shown as text —
        // if it ever renders empty the badge silently loses its colour.
        Assert.Contains("completed", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Period_closed_carries_its_invoice_when_one_is_supplied()
    {
        var (service, wire) = Build(new Dictionary<string, string> { ["Subject"] = "Pay Period Closed" });
        var pdf = new byte[] { 1, 2, 3, 4 };

        await service.SendPeriodClosedEmailAsync(
            Recipient, "Petr", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
            new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc), "2026-08",
            "en", pdf, "invoice-2026-08.pdf", CancellationToken.None);

        Assert.Contains("Petr", wire.Html, StringComparison.Ordinal);
        Assert.Contains("2026-08-01", wire.Html, StringComparison.Ordinal);
        Assert.Equal("invoice-2026-08.pdf", wire.AttachmentName);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Period_closed_without_an_invoice_attaches_nothing()
    {
        // The templated path branched into two different senders for this; the
        // rendered path takes a nullable attachment, so the no-invoice case is
        // worth pinning rather than assuming.
        var (service, wire) = Build(new Dictionary<string, string> { ["Subject"] = "Pay Period Closed" });

        await service.SendPeriodClosedEmailAsync(
            Recipient, "Petr", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
            new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc), "2026-08",
            "en", null, null, CancellationToken.None);

        Assert.Null(wire.AttachmentName);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Period_end_reminder_formats_the_countdown_from_its_translation()
    {
        var (service, wire) = Build(new Dictionary<string, string>
        {
            ["Subject"] = "Pay Period Ending Soon",
            ["DaysRemainingText"] = "{0} days remaining",
        });

        await service.SendPeriodEndReminderEmailAsync(
            Recipient, "Petr", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31),
            3, "2026-08", "en", CancellationToken.None);

        Assert.Contains("3 days remaining", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_body_declares_the_language_it_was_rendered_in()
    {
        // {{lang}} is on the <html> element of all seven templates and is in no
        // translation row, because it IS the row's language. Nothing but
        // BuildTemplateValues supplies it, so a regression renders lang="".
        var (service, wire) = Build(new Dictionary<string, string> { ["Subject"] = "Confirm Your Email" });

        await service.SendEmailConfirmationAsync(Recipient, "Jana", "483920", "uk", CancellationToken.None);

        Assert.Contains("lang=\"uk\"", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_support_address_falls_back_to_the_configured_sender()
    {
        // Without the fallback a locale missing one row leaves the customer with
        // "mailto:" and no address to reply to.
        var (service, wire) = Build(new Dictionary<string, string> { ["Subject"] = "Your Order Receipt" });

        await service.SendOrderReceiptEmailAsync(Recipient, NewOrder(), null, "r.pdf", "en", CancellationToken.None);

        Assert.Contains("noreply@example.test", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("mailto:\"", wire.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_seeded_support_address_beats_the_configured_sender()
    {
        var (service, wire) = Build(new Dictionary<string, string>
        {
            ["Subject"] = "Your Order Receipt",
            ["SupportEmail"] = "podpora@cleansia.cz",
        });

        await service.SendOrderReceiptEmailAsync(Recipient, NewOrder(), null, "r.pdf", "en", CancellationToken.None);

        Assert.Contains("podpora@cleansia.cz", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("noreply@example.test", wire.Html, StringComparison.Ordinal);
    }

    private static Order NewOrder() => Order.Create(
        customerName: "Jana Novakova",
        customerEmail: Recipient,
        customerPhone: "+420000000000",
        customerAddress: Address.Create("Dlouha 12", "Praha", "11000", "cz"),
        rooms: 3,
        bathrooms: 1,
        cleaningDateTime: new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc),
        paymentType: PaymentType.Card,
        totalPrice: 1250.00m,
        currencyId: "czk",
        paymentStatus: PaymentStatus.Pending);

    /// <summary>
    /// The promo e-mail with NO translation rows at all — which is the state the
    /// table has always been in, because EmailTemplateTranslation is admin-managed
    /// and nothing ever entered a PromoCode row.
    ///
    /// Owner, 2026-09-02: the delivered mail showed a lone "!" where the greeting
    /// belongs and a CTA button with no label. Both are what the renderer produces
    /// when it strips an unresolved placeholder: "{{Greeting}}!" collapses to "!"
    /// and the anchor empties. This is the regression test for that mail.
    /// </summary>
    [Fact]
    public async Task Promo_is_complete_with_no_translation_rows()
    {
        var (service, wire) = Build([]);

        await service.SendPromoCodeEmailAsync(
            "customer@example.com", "VITEJTE-JF8J6F", "-10 %",
            new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), "cs", CancellationToken.None);

        // The code and the figure are per-send data and were never the problem.
        Assert.Contains("VITEJTE-JF8J6F", wire.Html, StringComparison.Ordinal);
        Assert.Contains("-10 %", wire.Html, StringComparison.Ordinal);

        // The copy that used to vanish.
        Assert.Contains("Dobrý den", wire.Html, StringComparison.Ordinal);
        Assert.Contains("Objednat úklid", wire.Html, StringComparison.Ordinal);
        Assert.Contains("Kód platí do 2. 10. 2026.", wire.Html, StringComparison.Ordinal);
        Assert.Contains("tým Cleansia", wire.Html, StringComparison.Ordinal);
        Assert.Equal("Váš slevový kód Cleansia", wire.Subject);

        // The two shapes the owner actually saw in the inbox.
        Assert.DoesNotContain(">!<", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// An entered translation row still beats the default — the fallback is a
    /// floor, not an override. If this inverts, editing copy in the admin stops
    /// having any effect and nobody would notice until a customer read it.
    /// </summary>
    [Fact]
    public async Task Promo_translation_row_beats_the_default()
    {
        var (service, wire) = Build(new Dictionary<string, string>
        {
            ["Greeting"] = "Ahoj",
            ["ButtonText"] = "Chci uklidit",
        });

        await service.SendPromoCodeEmailAsync(
            "customer@example.com", "CODE-1", "-15 %", null, "cs", CancellationToken.None);

        Assert.Contains("Ahoj", wire.Html, StringComparison.Ordinal);
        Assert.Contains("Chci uklidit", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Dobrý den", wire.Html, StringComparison.Ordinal);
        // Keys the row did not carry still fall back rather than emptying.
        Assert.Contains("tým Cleansia", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unknown locale falls back to English rather than to nothing — the
    /// language code arrives off a user row and is not validated here.
    /// </summary>
    [Fact]
    public async Task Promo_falls_back_to_english_for_an_unknown_locale()
    {
        var (service, wire) = Build([]);

        await service.SendPromoCodeEmailAsync(
            "customer@example.com", "CODE-2", "-5 %", null, "de", CancellationToken.None);

        Assert.Contains("Hello", wire.Html, StringComparison.Ordinal);
        Assert.Contains("Book a clean", wire.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", wire.Html, StringComparison.Ordinal);
    }

    private static (EmailService Service, WireCapture Wire) Build(Dictionary<string, string> translations)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");
        config.SetupGet(c => c.ResetPasswordUrl).Returns("/reset-password");

        var repository = new Mock<IEmailTemplateTranslationRepository>();
        repository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string>(translations));

        var wire = new WireCapture();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(wire, disposeHandler: false));

        var service = new EmailService(
            config.Object,
            NullLogger<EmailService>.Instance,
            factory.Object,
            repository.Object,
            new EmailTemplateRenderer());

        return (service, wire);
    }

    /// <summary>Accepts the send and keeps what the SDK actually serialized.</summary>
    private sealed class WireCapture : HttpMessageHandler
    {
        public string Html { get; private set; } = string.Empty;
        public string Subject { get; private set; } = string.Empty;
        public string? AttachmentName { get; private set; }
        public string? AttachmentContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? "{}"
                : await request.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            Html = root.GetProperty("content")
                .EnumerateArray()
                .First(c => c.GetProperty("type").GetString() == "text/html")
                .GetProperty("value")
                .GetString() ?? string.Empty;

            // The SDK may carry the subject at the root or on the single
            // personalization depending on how the message was built; read both
            // so the assertion is about what SendGrid receives, not about which
            // field the helper happened to fill.
            Subject = root.TryGetProperty("subject", out var subject)
                ? subject.GetString() ?? string.Empty
                : root.TryGetProperty("personalizations", out var personalizations)
                  && personalizations.GetArrayLength() > 0
                  && personalizations[0].TryGetProperty("subject", out var personalSubject)
                    ? personalSubject.GetString() ?? string.Empty
                    : string.Empty;

            if (root.TryGetProperty("attachments", out var attachments) &&
                attachments.ValueKind == JsonValueKind.Array &&
                attachments.GetArrayLength() > 0)
            {
                var first = attachments[0];
                AttachmentName = first.GetProperty("filename").GetString();
                AttachmentContent = first.GetProperty("content").GetString();
            }

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
