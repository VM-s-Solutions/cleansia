using System.Net;
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
/// Owner rulings 2026-09-28. A cash booking is no longer answered with a receipt —
/// no money has moved — but with an informational e-mail in the customer's language: what to pay the
/// cleaner in cash, the slot, the address and the free-cancellation window. The receipt follows the clean,
/// and its e-mail thanks the customer for the clean rather than confirming a booking.
/// </summary>
public class OrderBookedEmailCopyTests
{
    private const string Recipient = "customer@example.com";

    [Theory]
    [InlineData("en", "Booking confirmed", "Please pay Kč1,234.50 in cash", "up to 24 h before")]
    [InlineData("cs", "Rezervace potvrzena", "Částku 1 234,50 Kč zaplaťte v hotovosti", "nejpozději 24 h před")]
    [InlineData("sk", "Rezervácia potvrdená", "Sumu 1 234,50 Kč zaplaťte v hotovosti", "najneskôr 24 h pred")]
    [InlineData("uk", "Бронювання підтверджено", "сплатіть 1 234,50 Kč готівкою", "за 24 год до")]
    [InlineData("ru", "Бронирование подтверждено", "оплатите 1 234,50 Kč наличными", "за 24 ч до")]
    public async Task A_Cash_Booking_Is_Told_What_To_Pay_In_Cash_And_Until_When_It_Cancels_Free(
        string language, string title, string cashLine, string cancellationLine)
    {
        var (service, values) = BuildService();

        await service.SendOrderBookedEmailAsync(Recipient, BuildOrder(), 24, language, CancellationToken.None);

        Assert.Equal(title, values["StatusLabel"]);
        Assert.Contains(title, values["Subject"]);
        Assert.Contains(cashLine, values["StatusMessage"]);
        Assert.Contains(cancellationLine, values["StatusMessage"]);
        Assert.Equal("Vinohradska 12, Praha", values["Address"]);
        Assert.False(string.IsNullOrWhiteSpace(values["CleaningDate"]));
        Assert.DoesNotContain("{{", string.Join(" ", values.Values));
    }

    [Theory]
    [InlineData("en", "Your receipt from Cleansia", "Thank you for your cleaning")]
    [InlineData("cs", "Účtenka za váš úklid", "účtenku za provedený úklid")]
    [InlineData("sk", "Účtenka za vaše upratovanie", "účtenku za vykonané upratovanie")]
    [InlineData("uk", "Квитанція за ваше прибирання", "Квитанцію за виконане прибирання")]
    [InlineData("ru", "Квитанция за вашу уборку", "Квитанция за выполненную уборку")]
    public async Task A_Receipt_Issued_After_The_Clean_Reads_As_A_Receipt_For_It(
        string language, string subject, string thanks)
    {
        var (service, values) = BuildService();
        var order = BuildOrder();
        order.MarkCashCollected("employee-1");
        order.MarkCompletedAt(DateTime.UtcNow);

        await service.SendOrderReceiptEmailAsync(Recipient, order, languageCode: language, ct: CancellationToken.None);

        Assert.Equal(subject, values["Subject"]);
        Assert.Contains(thanks, values["ThankYouText"]);
    }

    [Fact]
    public async Task A_Receipt_Issued_Before_The_Clean_Keeps_The_Booking_Copy()
    {
        var (service, values) = BuildService(new Dictionary<string, string>
        {
            ["Subject"] = "Your Order Receipt",
            ["ThankYouText"] = "We are pleased to confirm your booking.",
        });

        await service.SendOrderReceiptEmailAsync(Recipient, BuildOrder(), ct: CancellationToken.None);

        Assert.Equal("Your Order Receipt", values["Subject"]);
        Assert.Equal("We are pleased to confirm your booking.", values["ThankYouText"]);
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
            paymentType: PaymentType.Cash,
            totalPrice: 1234.5m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    /// <summary>The real service with a renderer double that keeps the values it was handed.</summary>
    private static (EmailService Service, Dictionary<string, string?> Values) BuildService(
        Dictionary<string, string>? translations = null)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string>(translations ?? []));

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
            Mock.Of<ICountryConfigurationRepository>());

        return (service, captured);
    }

    private sealed class AcceptingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}
