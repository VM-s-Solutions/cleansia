using System.Globalization;
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
/// The customer e-mails print the order's total with the order's own currency symbol. An order whose
/// <c>Currency</c> navigation was not loaded is a loader omission, not a CZK order — every order names
/// its <c>CurrencyId</c> — so the total is printed with NO unit rather than a guessed "Kč", which
/// would label a EUR sale as CZK in the customer's inbox.
/// </summary>
public class EmailServiceCurrencySymbolTests
{
    private const string Recipient = "customer@example.com";
    private const string GuestToken = "Bo0g5sHqYk3Xz9-A_1n2mQr4tUvWxYz6AbCdEfGhIjK";

    [Fact]
    public async Task The_Receipt_Email_Prints_The_Total_With_The_Orders_Own_Symbol()
    {
        var (service, values) = BuildService(EmailType.OrderReceipt);

        await service.SendOrderReceiptEmailAsync(Recipient, BuildOrder(Euro()), ct: CancellationToken.None);

        Assert.StartsWith("€", values["TotalAmount"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Receipt_Email_Prints_No_Unit_When_The_Currency_Was_Not_Loaded()
    {
        var (service, values) = BuildService(EmailType.OrderReceipt);

        await service.SendOrderReceiptEmailAsync(Recipient, BuildOrder(currency: null), ct: CancellationToken.None);

        Assert.Equal(1234.5m.ToString("N2", CultureInfo.CurrentCulture), values["TotalAmount"]);
        Assert.DoesNotContain("Kč", values["TotalAmount"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Status_Update_Email_Prints_The_Total_With_The_Orders_Own_Symbol()
    {
        var (service, values) = BuildService(EmailType.OrderStatusUpdate);

        await service.SendOrderStatusUpdateEmailAsync(Recipient, BuildOrder(Euro()), "confirmed", ct: CancellationToken.None);

        Assert.StartsWith("€", values["Total"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Status_Update_Email_Prints_No_Unit_When_The_Currency_Was_Not_Loaded()
    {
        var (service, values) = BuildService(EmailType.OrderStatusUpdate);

        await service.SendOrderStatusUpdateEmailAsync(Recipient, BuildOrder(currency: null), "confirmed", ct: CancellationToken.None);

        Assert.Equal(1234.5m.ToString("N2", CultureInfo.CurrentCulture), values["Total"]);
        Assert.DoesNotContain("Kč", values["Total"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en", "Your order has been cancelled.", "Refund issued:")]
    [InlineData("cs", "Vaše rezervace byla zrušena.", "Vrácená částka:")]
    [InlineData("sk", "Vaša rezervácia bola zrušená.", "Vrátená suma:")]
    [InlineData("uk", "Ваше бронювання скасовано.", "Сума повернення:")]
    [InlineData("ru", "Ваше бронирование отменено.", "Сумма возврата:")]
    public async Task Guest_cancellation_email_localizes_copy_and_only_prints_actual_successful_refund(
        string language, string cancelled, string refundLabel)
    {
        var order = BuildOrder(Euro());
        var (service, values) = BuildService(EmailType.OrderStatusUpdate);
        await service.SendOrderStatusUpdateEmailAsync(Recipient, order, "Cancelled", language,
            CancellationToken.None, 400m, guestAccessToken: GuestToken);
        Assert.Contains(cancelled, values["StatusMessage"]);
        Assert.Contains(refundLabel, values["StatusMessage"]);
        Assert.Contains($"€{400m:N2}", values["StatusMessage"]);
        Assert.DoesNotContain($"{order.TotalPrice:N2}", values["StatusMessage"]);
        Assert.Contains($"token={Uri.EscapeDataString(GuestToken)}", values["OrderStatusLink"]);
        Assert.DoesNotContain(order.ConfirmationCode, values["OrderStatusLink"], StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(values["ButtonText"]));
        Assert.False(string.IsNullOrWhiteSpace(values["StatusLabel"]));
        Assert.DoesNotContain("{{", string.Join(" ", values.Values));

        await service.SendOrderStatusUpdateEmailAsync(Recipient, order, "Cancelled", language,
            CancellationToken.None, null);
        Assert.DoesNotContain("token=", values["OrderStatusLink"], StringComparison.Ordinal);
        Assert.Equal(cancelled, values["StatusMessage"]);
        Assert.DoesNotContain(refundLabel, values["StatusMessage"]);
    }

    private static Order BuildOrder(Currency? currency)
    {
        var address = Address.Create("Hauptstr. 2", "Berlin", "10115", "de");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: Recipient,
            customerPhone: "+490000000000",
            customerAddress: address,
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1234.5m,
            currencyId: "eur",
            paymentStatus: PaymentStatus.Pending);
        if (currency is not null)
        {
            order.SetCurrency(currency);
        }
        return order;
    }

    private static Currency Euro()
    {
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = "eur";
        return eur;
    }

    /// <summary>
    /// The real service over an accepting SendGrid endpoint, with a renderer double that keeps the
    /// values dictionary it was handed — the one place the formatted total is visible before it is
    /// folded into HTML.
    /// </summary>
    private static (EmailService Service, Dictionary<string, string?> Values) BuildService(EmailType emailType)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                emailType, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

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
            renderer.Object);

        return (service, captured);
    }

    private sealed class AcceptingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}
