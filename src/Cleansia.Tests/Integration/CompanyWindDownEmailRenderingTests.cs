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
/// What goes out on the wire for the two company wind-down notices (ADR-0064 D2 step 1): every
/// locale renders with the company named as its receipts name it and the last day of service, no
/// placeholder survives, and an admin translation row wins over the in-code default the way it does
/// for the promo e-mail.
/// </summary>
public class CompanyWindDownEmailRenderingTests
{
    private const string Recipient = "customer@example.com";
    private static readonly IReadOnlyList<string> Companies = ["Cleansia CZ s.r.o.", "Cleansia SK s.r.o."];
    private static readonly DateOnly From = new(2026, 10, 1);

    public static TheoryData<string> Locales => ["en", "cs", "sk", "uk", "ru"];

    [Theory]
    [MemberData(nameof(Locales))]
    public async Task The_customer_notice_names_every_market_and_the_date_in_each_locale(string locale)
    {
        var (service, capture) = BuildService(EmailType.CompanyWindDownCustomer, []);

        await service.SendCompanyWindDownCustomerNoticeAsync(Recipient, "Jana", Companies, From, locale, CancellationToken.None);

        AssertRendered(capture, locale);
    }

    [Theory]
    [MemberData(nameof(Locales))]
    public async Task The_cleaner_notice_names_every_market_and_the_date_in_each_locale(string locale)
    {
        var (service, capture) = BuildService(EmailType.CompanyWindDownCleaner, []);

        await service.SendCompanyWindDownCleanerNoticeAsync(Recipient, "Petr", Companies, From, locale, CancellationToken.None);

        AssertRendered(capture, locale);
    }

    [Fact]
    public async Task An_unknown_locale_falls_back_to_English()
    {
        var (service, capture) = BuildService(EmailType.CompanyWindDownCustomer, []);

        await service.SendCompanyWindDownCustomerNoticeAsync(Recipient, "Jana", Companies, From, "de", CancellationToken.None);

        Assert.Contains("is winding down its operations", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("Cleansia CZ s.r.o., Cleansia SK s.r.o.", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_admin_translation_row_wins_over_the_default()
    {
        var (service, capture) = BuildService(EmailType.CompanyWindDownCustomer, new Dictionary<string, string>
        {
            ["IntroText"] = "{0} closes its doors on {1}.",
            ["CreditText"] = "Credit is gone at the close.",
        });

        await service.SendCompanyWindDownCustomerNoticeAsync(Recipient, "Jana", Companies, From, "en", CancellationToken.None);

        Assert.Contains("Cleansia CZ s.r.o., Cleansia SK s.r.o. closes its doors on 1. 10. 2026.", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("Credit is gone at the close.", capture.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Any unspent credit", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_cleaner_notice_says_where_to_export_or_erase_and_that_partner_sign_in_ends()
    {
        var (service, capture) = BuildService(EmailType.CompanyWindDownCleaner, []);

        await service.SendCompanyWindDownCleanerNoticeAsync(Recipient, "Petr", Companies, From, "en", CancellationToken.None);

        Assert.Contains("signing in to the partner app will no longer be possible", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("sign in to the customer app", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("last pay period", capture.HtmlContent, StringComparison.Ordinal);
    }

    private static void AssertRendered(BodyCapturingHandler capture, string locale)
    {
        var html = capture.HtmlContent;
        Assert.Contains("Cleansia CZ s.r.o., Cleansia SK s.r.o.", html, StringComparison.Ordinal);
        Assert.Contains("1. 10. 2026", html, StringComparison.Ordinal);
        Assert.Contains($"lang=\"{locale}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{1}", html, StringComparison.Ordinal);
        Assert.Contains("noreply@example.test", html, StringComparison.Ordinal);
        Assert.Contains("https://app.test", html, StringComparison.Ordinal);
    }

    private static (EmailService Service, BodyCapturingHandler Capture) BuildService(
        EmailType emailType,
        Dictionary<string, string> translations)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(emailType, It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
