using System.Net;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The status e-mails a take, a start and a completion send arrive in the booking's language, labels
/// included: the in-code copy covers every arm, so nothing falls back to English for a Czech customer, and
/// the Confirmed arm says what it has meant since ADR-0057 — a cleaner took the booking — instead of
/// repeating the booking e-mail's title. An admin-entered translation row still wins.
/// </summary>
public class OrderStatusEmailCopyTests
{
    private const string Recipient = "customer@example.com";

    public static TheoryData<string, string, string, string> EveryArmInEveryLocale => new()
    {
        { "Confirmed", "en", "Cleaner found", "A cleaner has taken your booking." },
        { "Confirmed", "cs", "Uklízeč nalezen", "Vaši rezervaci převzal uklízeč." },
        { "Confirmed", "sk", "Upratovač nájdený", "Vašu rezerváciu prevzal upratovač." },
        { "Confirmed", "uk", "Прибиральника знайдено", "Ваше бронювання взяв прибиральник." },
        { "Confirmed", "ru", "Уборщик найден", "Ваше бронирование взял уборщик." },
        { "Started", "en", "Cleaning started", "Your cleaning session has started." },
        { "Started", "cs", "Úklid začal", "Váš úklid právě začal." },
        { "Started", "sk", "Upratovanie začalo", "Vaše upratovanie práve začalo." },
        { "Started", "uk", "Прибирання розпочалося", "Ваше прибирання розпочалося." },
        { "Started", "ru", "Уборка началась", "Ваша уборка началась." },
        { "Completed", "en", "Cleaning complete", "Your cleaning has been completed successfully." },
        { "Completed", "cs", "Úklid dokončen", "Váš úklid byl úspěšně dokončen." },
        { "Completed", "sk", "Upratovanie dokončené", "Vaše upratovanie bolo úspešne dokončené." },
        { "Completed", "uk", "Прибирання завершено", "Ваше прибирання успішно завершено." },
        { "Completed", "ru", "Уборка завершена", "Ваша уборка успешно завершена." },
    };

    private static readonly Dictionary<string, (string OrderNumber, string Section, string Button)> Labels = new()
    {
        ["en"] = ("Order #", "Current status", "View order details"),
        ["cs"] = ("Rezervace č.", "Aktuální stav", "Zobrazit detail rezervace"),
        ["sk"] = ("Rezervácia č.", "Aktuálny stav", "Zobraziť detail rezervácie"),
        ["uk"] = ("Бронювання №", "Поточний статус", "Переглянути бронювання"),
        ["ru"] = ("Бронирование №", "Текущий статус", "Посмотреть бронирование"),
    };

    [Theory]
    [MemberData(nameof(EveryArmInEveryLocale))]
    public async Task Every_Arm_Is_Written_In_The_Bookings_Language(
        string status, string language, string title, string message)
    {
        var (service, values) = BuildService();

        await service.SendOrderStatusUpdateEmailAsync(Recipient, BuildOrder(), status, language, CancellationToken.None);

        Assert.Equal(title, values["StatusLabel"]);
        Assert.Contains(title, values["Subject"]);
        Assert.Contains(Labels[language].OrderNumber, values["Subject"]);
        Assert.Equal(message, values["StatusMessage"]);
        Assert.Equal(Labels[language].Section, values["StatusSectionLabel"]);
        Assert.Equal(Labels[language].Button, values["ButtonText"]);
        Assert.DoesNotContain("{{", string.Join(" ", values.Values));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("cs")]
    [InlineData("sk")]
    [InlineData("uk")]
    [InlineData("ru")]
    public async Task The_Cleaner_Found_Title_Is_Never_The_Booking_Emails_Title(string language)
    {
        var (service, values) = BuildService();

        await service.SendOrderStatusUpdateEmailAsync(Recipient, BuildOrder(), "Confirmed", language, CancellationToken.None);
        var confirmed = values["StatusLabel"];
        await service.SendOrderBookedEmailAsync(Recipient, BuildOrder(), freeCancellationHours: 24, language, CancellationToken.None);

        Assert.NotEqual(values["StatusLabel"], confirmed);
    }

    [Fact]
    public async Task An_Admin_Translation_Row_Still_Wins_Over_The_In_Code_Copy()
    {
        var (service, values) = BuildService(new Dictionary<string, string> { ["StatusTitle_Completed"] = "All done" });

        await service.SendOrderStatusUpdateEmailAsync(Recipient, BuildOrder(), "Completed", "cs", CancellationToken.None);

        Assert.Equal("All done", values["StatusLabel"]);
        Assert.Equal("Váš úklid byl úspěšně dokončen.", values["StatusMessage"]);
    }

    private static Order BuildOrder()
    {
        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: Recipient,
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1234.5m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = "order-status-copy-1";
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    /// <summary>The real service with a renderer double that keeps the values it was handed.</summary>
    private static (EmailService Service, Dictionary<string, string?> Values) BuildService(
        Dictionary<string, string>? rows = null)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string>(rows ?? []));

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new AcceptingHandler(), disposeHandler: false));

        var captured = new Dictionary<string, string?>(StringComparer.Ordinal);
        var renderer = new Mock<IEmailTemplateRenderer>();
        renderer
            .Setup(r => r.Render(It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Callback((string _, IReadOnlyDictionary<string, string?> values) =>
            {
                captured.Clear();
                foreach (var (key, value) in values)
                {
                    captured[key] = value;
                }
            })
            .Returns("<html></html>");

        var service = new EmailService(
            config.Object,
            NullLogger<EmailService>.Instance,
            httpClientFactory.Object,
            translationRepository.Object,
            renderer.Object,
            Mock.Of<ICountryConfigurationRepository>(),
            Mock.Of<ICompanyInfoRepository>());

        return (service, captured);
    }

    private sealed class AcceptingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}
