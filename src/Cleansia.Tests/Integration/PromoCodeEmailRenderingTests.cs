using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Integration;

/// <summary>
/// What actually goes out on the wire for the first-order promo e-mail.
///
/// <para>The body is assembled from two sources that are easy to get the wrong way
/// round: the per-locale copy in <c>EmailTemplateTranslation</c>, and the per-send
/// data (the code, the figure, the expiry). Merging the translations LAST silently
/// overwrote the discount figure with the untranslated phrase, and the e-mail went
/// out offering a discount without ever saying how much — a defect nothing observed,
/// because every value in it was present and plausible.</para>
///
/// <para>These assert on the rendered HTML the SDK is handed, which is the only place
/// that mistake is visible.</para>
/// </summary>
public class PromoCodeEmailRenderingTests
{
    private const string Recipient = "visitor@example.com";
    private const string Code = "VITEJTE-K7Q2MX";

    [Fact]
    public async Task The_body_carries_both_the_figure_and_the_translated_phrase()
    {
        var (service, capture) = BuildService(new Dictionary<string, string>
        {
            ["Subject"] = "Váš slevový kód Cleansia",
            ["DiscountText"] = "Sleva na první objednávku",
        });

        await service.SendPromoCodeEmailAsync(Recipient, Code, "-10 %", expiresOn: null, "cs", CancellationToken.None);

        var html = capture.HtmlContent;
        Assert.Contains("-10 %", html, StringComparison.Ordinal);
        Assert.Contains("Sleva na první objednávku", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_code_reaches_the_body_and_no_placeholder_survives()
    {
        var (service, capture) = BuildService(new Dictionary<string, string> { ["Subject"] = "Your code" });

        await service.SendPromoCodeEmailAsync(Recipient, Code, "-10 %", expiresOn: null, "en", CancellationToken.None);

        Assert.Contains(Code, capture.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absent_expiry_renders_nothing_rather_than_a_fabricated_date()
    {
        var (service, capture) = BuildService(new Dictionary<string, string>
        {
            ["Subject"] = "Your code",
            ["ExpiryNotice"] = "The code is valid until {0}.",
        });

        await service.SendPromoCodeEmailAsync(Recipient, Code, "-10 %", expiresOn: null, "en", CancellationToken.None);

        Assert.DoesNotContain("valid until", capture.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_expiry_is_formatted_into_the_translated_sentence()
    {
        var (service, capture) = BuildService(new Dictionary<string, string>
        {
            ["Subject"] = "Your code",
            ["ExpiryNotice"] = "The code is valid until {0}.",
        });

        await service.SendPromoCodeEmailAsync(
            Recipient, Code, "-10 %", new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), "en", CancellationToken.None);

        Assert.Contains("valid until 30. 9. 2026.", capture.HtmlContent, StringComparison.Ordinal);
    }

    private static (EmailService Service, BodyCapturingHandler Capture) BuildService(
        Dictionary<string, string> translations)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                EmailType.PromoCode, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(translations);

        var capture = new BodyCapturingHandler();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(capture, disposeHandler: false));

        var service = new EmailService(
            config.Object,
            NullLogger<EmailService>.Instance,
            httpClientFactory.Object,
            translationRepository.Object,
            new EmailTemplateRenderer());

        return (service, capture);
    }

    /// <summary>Accepts the send and keeps the HTML content the SDK put on the wire.</summary>
    private sealed class BodyCapturingHandler : HttpMessageHandler
    {
        public string HtmlContent { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? "{}"
                : await request.Content.ReadAsStringAsync(cancellationToken);

            using var document = JsonDocument.Parse(body);
            HtmlContent = document.RootElement
                .GetProperty("content")
                .EnumerateArray()
                .First(c => c.GetProperty("type").GetString() == "text/html")
                .GetProperty("value")
                .GetString() ?? string.Empty;

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
