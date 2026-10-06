using System.Net;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Services.Pdf.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Owner ruling 2026-10-06, in the five languages an e-mail is sent in: what a customer owes refuses every new
/// booking, so the pay-link e-mail no longer promises that card stays open; and the customer who did not pay
/// the cleaner at the door is told what is owed and sent to the order's page, where it is paid.
/// </summary>
public sealed class ReceivableEmailCopyTests
{
    private const string Recipient = "customer@example.com";

    public static TheoryData<string, string> NoNewBooking => new()
    {
        { "en", "Until it is paid, you cannot make a new booking." },
        { "cs", "Dokud nebude zaplacena, nelze vytvořit novou objednávku." },
        { "sk", "Kým nebude zaplatená, nie je možné vytvoriť novú objednávku." },
        { "uk", "Доки суму не сплачено, нові бронювання недоступні." },
        { "ru", "Пока сумма не оплачена, новые бронирования недоступны." },
    };

    [Theory]
    [MemberData(nameof(NoNewBooking))]
    public async Task The_Pay_Link_E_Mail_Says_No_New_Booking_Is_Taken_Until_It_Is_Paid(string language, string sentence)
    {
        var (service, values) = BuildService();
        var order = BuildOrder();

        await service.SendReceivablePayLinkEmailAsync(
            Recipient, order, Receivable.ForCashCancellationFee(order, 300m), "https://pay.test/r", language, CancellationToken.None);

        Assert.EndsWith(sentence, values["StatusMessage"]);
        Assert.Equal("https://pay.test/r", values["OrderStatusLink"]);
    }

    [Theory]
    [MemberData(nameof(NoNewBooking))]
    public async Task The_Cash_Not_Paid_E_Mail_Names_The_Debt_And_Opens_The_Orders_Page(string language, string sentence)
    {
        var (service, values) = BuildService();
        var order = BuildOrder();
        var debt = Receivable.ForUnpaidCash(order);

        await service.SendOrderCashNotPaidEmailAsync(Recipient, order, debt, language, CancellationToken.None);

        Assert.Contains(sentence, values["StatusMessage"]);
        Assert.Contains(order.DisplayOrderNumber, values["Subject"]);
        Assert.Equal($"https://app.test/orders/{order.Id}", values["OrderStatusLink"]);
        Assert.Equal(ReceiptLabels.For(language).ReceivableKinds[ReceivableKind.UnpaidCash], values["StatusLabel"]);
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
            cleaningDateTime: DateTime.UtcNow.AddHours(-3),
            paymentType: PaymentType.Cash,
            totalPrice: 1234.5m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    private static (EmailService Service, Dictionary<string, string?> Values) BuildService()
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

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
            Mock.Of<IEmailTemplateTranslationRepository>(),
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
