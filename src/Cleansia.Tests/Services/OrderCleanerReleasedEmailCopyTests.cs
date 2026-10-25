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
/// Owner ruling 2026-10-25: the customer is told when their booking goes back to New. The status e-mail's
/// cleaner-released arm says so in the customer's language, in the copy the code carries: it names no
/// cleaner, gives no reason, promises no time and no money, and links the booking the way every status
/// e-mail does.
/// </summary>
public class OrderCleanerReleasedEmailCopyTests
{
    private const string Recipient = "customer@example.com";

    [Theory]
    [InlineData("en", "We're finding you a new cleaner",
        "The cleaner booked for your cleaning can no longer come. We're looking for another cleaner and will let you know as soon as one takes your booking.")]
    [InlineData("cs", "Hledáme vám nového uklízeče",
        "Uklízeč objednaný na váš úklid už nemůže přijít. Hledáme jiného uklízeče a dáme vám vědět, jakmile vaši rezervaci někdo převezme.")]
    [InlineData("sk", "Hľadáme vám nového upratovača",
        "Upratovač objednaný na vaše upratovanie už nemôže prísť. Hľadáme iného upratovača a dáme vám vedieť, hneď ako vašu rezerváciu niekto prevezme.")]
    [InlineData("uk", "Шукаємо для вас нового прибиральника",
        "Прибиральник, призначений на ваше прибирання, більше не може прийти. Ми шукаємо іншого прибиральника й повідомимо вас, щойно хтось візьме ваше бронювання.")]
    [InlineData("ru", "Ищем для вас нового уборщика",
        "Уборщик, назначенный на вашу уборку, больше не может прийти. Мы ищем другого уборщика и сообщим вам, как только кто-нибудь возьмёт ваше бронирование.")]
    public async Task The_Customer_Is_Told_In_Their_Language_That_Another_Cleaner_Is_Being_Looked_For(
        string language, string title, string message)
    {
        var (service, values) = BuildService();

        await service.SendOrderStatusUpdateEmailAsync(
            Recipient, BuildOrder(userId: "user-1"), "CleanerReleased", language, CancellationToken.None);

        Assert.Equal(title, values["StatusLabel"]);
        Assert.Contains(title, values["Subject"]);
        Assert.Equal(message, values["StatusMessage"]);
        Assert.DoesNotContain("{{", string.Join(" ", values.Values));
    }

    [Fact]
    public async Task A_Booking_With_An_Account_Links_Its_Order_And_A_Guest_Booking_Its_Track_Page_With_The_Token()
    {
        var (service, values) = BuildService();
        var account = BuildOrder(userId: "user-1");
        var guest = BuildOrder(userId: null);

        await service.SendOrderStatusUpdateEmailAsync(
            Recipient, account, "CleanerReleased", "en", CancellationToken.None);
        var accountLink = values["OrderStatusLink"];
        await service.SendOrderStatusUpdateEmailAsync(
            Recipient, guest, "CleanerReleased", "en", CancellationToken.None, guestAccessToken: "guest-token");

        Assert.Equal("https://app.test/orders/order-released-1", accountLink);
        Assert.Equal(
            $"https://app.test/track-order?orderNumber={Uri.EscapeDataString(guest.DisplayOrderNumber)}"
                + $"&email={Uri.EscapeDataString(Recipient)}&token=guest-token",
            values["OrderStatusLink"]);
    }

    private static Order BuildOrder(string? userId)
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
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = "order-released-1";
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    /// <summary>The real service with a renderer double that keeps the values it was handed.</summary>
    private static (EmailService Service, Dictionary<string, string?> Values) BuildService()
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string>());

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
