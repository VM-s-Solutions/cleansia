using System.Globalization;
using System.Net;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.AdminNotifications;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.AdminNotifications;

/// <summary>
/// What goes out on the wire for an admin notification: every one of the nine events renders in
/// every one of the five locales from the one template with no placeholder left, the subject and
/// the body substitute the args in the order the catalogue entry declares, a timestamp and a day are
/// shown as dates rather than ISO strings, the crew-lost copy says the clean was under way when it
/// was, an admin translation row wins over the in-code default the way it does for the wind-down
/// notices, and an unknown locale falls back to English.
/// </summary>
public sealed class AdminNotificationEmailRenderingTests
{
    private const string Recipient = "ops@example.com";

    private static readonly string[] Locales = ["en", "cs", "sk", "uk", "ru"];

    // The theory over the whole grid is also the proof the copy is complete: a locale missing a
    // key's subject or body, or a placeholder past the entry's arg count, throws before the send.
    public static TheoryData<string, string> EveryKeyInEveryLocale
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var key in AdminNotificationEventCatalog.All)
            {
                foreach (var locale in Locales)
                {
                    data.Add(key, locale);
                }
            }

            return data;
        }
    }

    private static IReadOnlyDictionary<string, string> SampleArgs(string eventKey) =>
        AdminEventCatalog.Find(eventKey).EmailArgOrder.ToDictionary(name => name, name => name switch
        {
            "orderNumber" => "ORD-1A2B3C4D",
            "amount" => "1 250 Kč",
            "paymentType" => nameof(PaymentType.Card),
            "countryId" => "country-cz",
            "orderId" => "order-1",
            "cause" => "dropped",
            "statusAtLoss" => nameof(OrderStatus.Confirmed),
            "cleaningDateTime" => new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            "disputeId" => "dispute-1",
            "reason" => nameof(DisputeReason.QualityIssue),
            "requestId" => "request-1",
            "day" => "2026-10-01",
            "windDownFrom" => "2026-10-01",
            "cancelled" => "3",
            "refunded" => "2",
            "refundFailures" => "1",
            "periodsClosed" => "1",
            "archivedOn" => "2026-10-01",
            _ => throw new ArgumentOutOfRangeException(nameof(eventKey), name, "No sample for this arg."),
        }, StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(EveryKeyInEveryLocale))]
    public async Task Every_Event_Renders_In_Every_Locale_With_No_Placeholder_Left(string eventKey, string locale)
    {
        var (service, capture) = BuildService([]);

        await service.SendAdminNotificationEmailAsync(Recipient, eventKey, SampleArgs(eventKey), locale, CancellationToken.None);

        var html = capture.HtmlContent;
        Assert.Contains($"lang=\"{locale}\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{0}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{1}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{2}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{3}", html, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-01T", html, StringComparison.Ordinal);
        Assert.Contains("noreply@example.test", html, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(capture.Subject));
        Assert.DoesNotContain("{0}", capture.Subject, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", capture.Subject, StringComparison.Ordinal);
        Assert.Contains(capture.Subject, html, StringComparison.Ordinal);
        if (eventKey.StartsWith("admin.order.", StringComparison.Ordinal) || eventKey.StartsWith("admin.dispute.", StringComparison.Ordinal) || eventKey.StartsWith("admin.payment.", StringComparison.Ordinal))
        {
            Assert.Contains("ORD-1A2B3C4D", capture.Subject, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task The_Czech_Crew_Lost_Email_Names_The_Order_And_The_Time_And_Says_The_Clean_Was_Under_Way()
    {
        var (service, capture) = BuildService([]);
        var args = new Dictionary<string, string>(SampleArgs(AdminNotificationEventCatalog.OrderCrewLost))
        {
            ["statusAtLoss"] = nameof(OrderStatus.OnTheWay),
        };

        await service.SendAdminNotificationEmailAsync(Recipient, AdminNotificationEventCatalog.OrderCrewLost, args, "cs", CancellationToken.None);

        Assert.Equal("Objednávka ORD-1A2B3C4D přišla o posledního uklízeče", capture.Subject);
        Assert.Contains("ORD-1A2B3C4D", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("01.10.2026 09:30 UTC", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("Úklid už probíhal.", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("uklízeč ji opustil", capture.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Je zpět na nástěnce.", capture.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(OrderStatus.Confirmed), "It is back on the board.")]
    [InlineData(nameof(OrderStatus.New), "It is back on the board.")]
    [InlineData(nameof(OrderStatus.OnTheWay), "The clean was already under way.")]
    [InlineData(nameof(OrderStatus.InProgress), "The clean was already under way.")]
    public async Task The_Crew_Lost_Body_Follows_The_Status_At_Loss(string statusAtLoss, string expected)
    {
        var (service, capture) = BuildService([]);
        var args = new Dictionary<string, string>(SampleArgs(AdminNotificationEventCatalog.OrderCrewLost))
        {
            ["statusAtLoss"] = statusAtLoss,
            ["cause"] = "rejected",
        };

        await service.SendAdminNotificationEmailAsync(Recipient, AdminNotificationEventCatalog.OrderCrewLost, args, "en", CancellationToken.None);

        Assert.Contains(expected, capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("the cleaner's account was rejected", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Day_Arg_Is_Shown_As_A_Date_And_A_Count_As_Itself()
    {
        var (service, capture) = BuildService([]);

        await service.SendAdminNotificationEmailAsync(Recipient, AdminNotificationEventCatalog.CompanyArchived, SampleArgs(AdminNotificationEventCatalog.CompanyArchived), "en", CancellationToken.None);
        Assert.Contains("The books were sealed on 1. 10. 2026.", capture.HtmlContent, StringComparison.Ordinal);

        await service.SendAdminNotificationEmailAsync(Recipient, AdminNotificationEventCatalog.CompanyWindDownRun, SampleArgs(AdminNotificationEventCatalog.CompanyWindDownRun), "en", CancellationToken.None);
        Assert.Contains("Cancelled 3, refunded 2, refund failures 1, pay periods closed 1.", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Admin_Translation_Row_Wins_Over_The_Default()
    {
        var (service, capture) = BuildService(new Dictionary<string, string>
        {
            ["admin.dispute.filed.Subject"] = "Dispute on {0} — please look",
            ["HintText"] = "Log in and answer within a day.",
        });

        await service.SendAdminNotificationEmailAsync(Recipient, AdminNotificationEventCatalog.DisputeFiled, SampleArgs(AdminNotificationEventCatalog.DisputeFiled), "en", CancellationToken.None);

        Assert.Equal("Dispute on ORD-1A2B3C4D — please look", capture.Subject);
        Assert.Contains("Log in and answer within a day.", capture.HtmlContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign in to the admin console", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Contains("A customer filed a dispute on order ORD-1A2B3C4D.", capture.HtmlContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Unknown_Locale_Falls_Back_To_English()
    {
        var (service, capture) = BuildService([]);

        await service.SendAdminNotificationEmailAsync(Recipient, AdminNotificationEventCatalog.PaymentFailed, SampleArgs(AdminNotificationEventCatalog.PaymentFailed), "de", CancellationToken.None);

        Assert.Contains("lang=\"en\"", capture.HtmlContent, StringComparison.Ordinal);
        Assert.Equal("A card payment failed on order ORD-1A2B3C4D", capture.Subject);
    }

    [Fact]
    public async Task A_Key_Outside_The_Catalogue_Sends_Nothing_And_Throws()
    {
        var (service, capture) = BuildService([]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.SendAdminNotificationEmailAsync(Recipient, NotificationEventCatalog.OrderConfirmed, new Dictionary<string, string>(), "en", CancellationToken.None));

        Assert.Equal(string.Empty, capture.HtmlContent);
    }

    private static (EmailService Service, BodyCapturingHandler Capture) BuildService(Dictionary<string, string> translations)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(EmailType.AdminNotification, It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

        public string Subject { get; private set; } = string.Empty;

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
            Subject = document.RootElement.TryGetProperty("subject", out var subject)
                ? subject.GetString() ?? string.Empty
                : document.RootElement.GetProperty("personalizations").EnumerateArray().First().GetProperty("subject").GetString() ?? string.Empty;

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
