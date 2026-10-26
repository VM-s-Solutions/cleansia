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
/// Owner ruling 2026-10-25: the customer is told when a cleaner leaves their booking. The status e-mail's
/// cleaner-released arm says so in the customer's language, in the copy the code carries: it counts no
/// cleaners, so it stays true whether the last cleaner left or one of two did, names no cleaner, gives no
/// reason, promises no time and no money and no follow-up message, and links the booking the way every
/// status e-mail does (owner decision 2026-10-25).
/// </summary>
public class OrderCleanerReleasedEmailCopyTests
{
    private const string Recipient = "customer@example.com";

    [Theory]
    [InlineData("en", "A cleaner can no longer come",
        "Your booking still stands. We're offering it to our cleaners again.")]
    [InlineData("cs", "Uklízeč už nemůže přijít",
        "Vaše rezervace dál platí. Znovu ji nabízíme našim uklízečům.")]
    [InlineData("sk", "Upratovač už nemôže prísť",
        "Vaša rezervácia naďalej platí. Opäť ju ponúkame našim upratovačom.")]
    [InlineData("uk", "Прибиральник більше не може прийти",
        "Ваше бронювання залишається в силі. Ми знову пропонуємо його нашим прибиральникам.")]
    [InlineData("ru", "Уборщик больше не может прийти",
        "Ваше бронирование остаётся в силе. Мы снова предлагаем его нашим уборщикам.")]
    public async Task The_Customer_Is_Told_In_Their_Language_That_The_Booking_Is_Offered_To_Cleaners_Again(
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
