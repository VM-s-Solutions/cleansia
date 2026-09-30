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
/// The e-mails that carry a contract confirmation (decision X4): the booking e-mail goes to a card booking
/// too, once its payment concludes the contract, and does not ask it to pay in cash; with the confirmation
/// attached it says so in the customer's language; the cleaner's copy of a contract for work names the job
/// in the cleaner's language; and SendGrid receives each PDF under its file name.
/// </summary>
public sealed class ContractConfirmationEmailCopyTests
{
    private const string Recipient = "customer@example.com";
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46];

    [Theory]
    [InlineData("en", "Booking confirmed", "in cash", "Your booking confirmation is attached as a PDF.")]
    [InlineData("cs", "Rezervace potvrzena", "v hotovosti", "Potvrzení rezervace najdete v příloze jako PDF.")]
    [InlineData("sk", "Rezervácia potvrdená", "v hotovosti", "Potvrdenie rezervácie nájdete v prílohe ako PDF.")]
    [InlineData("uk", "Бронювання підтверджено", "готівкою", "Підтвердження бронювання додано до листа у форматі PDF.")]
    [InlineData("ru", "Бронирование подтверждено", "наличными", "Подтверждение бронирования приложено к письму в формате PDF.")]
    public async Task A_Card_Booking_Is_Confirmed_With_The_Pdf_And_Not_Asked_To_Pay_In_Cash(
        string language, string title, string cashWord, string attachedLine)
    {
        var (service, values, requests) = BuildService();

        await service.SendOrderBookedEmailAsync(
            Recipient, BuildOrder(PaymentType.Card), 24, language, CancellationToken.None,
            confirmationPdf: Pdf, confirmationFileName: "booking-confirmation-ORD-1.pdf");

        Assert.Equal(title, values["StatusLabel"]);
        Assert.DoesNotContain(cashWord, values["StatusMessage"]);
        Assert.Contains(attachedLine, values["StatusMessage"]);
        Assert.Contains("booking-confirmation-ORD-1.pdf", Assert.Single(requests));
    }

    [Fact]
    public async Task A_Cash_Booking_Is_Still_Asked_To_Pay_In_Cash_Beside_The_Pdf()
    {
        var (service, values, _) = BuildService();

        await service.SendOrderBookedEmailAsync(
            Recipient, BuildOrder(PaymentType.Cash), 24, "en", CancellationToken.None,
            confirmationPdf: Pdf, confirmationFileName: "booking-confirmation-ORD-1.pdf");

        Assert.Contains("Please pay Kč1,234.50 in cash", values["StatusMessage"]);
        Assert.Contains("Your booking confirmation is attached as a PDF.", values["StatusMessage"]);
    }

    [Fact]
    public async Task Without_A_Pdf_The_Booking_Email_Promises_None()
    {
        var (service, values, requests) = BuildService();

        await service.SendOrderBookedEmailAsync(Recipient, BuildOrder(PaymentType.Cash), 24, "en", CancellationToken.None);

        Assert.DoesNotContain("attached", values["StatusMessage"]);
        Assert.DoesNotContain("\"attachments\"", Assert.Single(requests));
    }

    [Theory]
    [InlineData("en", "Your contract for work for job ORD-7Q2K", "Hello Petra Dvořáková", "You accepted the contract for work for job ORD-7Q2K.")]
    [InlineData("cs", "Vaše smlouva o dílo k zakázce ORD-7Q2K", "Dobrý den, Petra Dvořáková", "Přijali jste smlouvu o dílo k zakázce ORD-7Q2K.")]
    [InlineData("sk", "Vaša zmluva o dielo k zákazke ORD-7Q2K", "Dobrý deň, Petra Dvořáková", "Prijali ste zmluvu o dielo k zákazke ORD-7Q2K.")]
    [InlineData("uk", "Ваш договір підряду на замовлення ORD-7Q2K", "Вітаємо, Petra Dvořáková", "Ви прийняли договір підряду на замовлення ORD-7Q2K.")]
    [InlineData("ru", "Ваш договор подряда на заказ ORD-7Q2K", "Здравствуйте, Petra Dvořáková", "Вы приняли договор подряда на заказ ORD-7Q2K.")]
    public async Task The_Cleaner_Is_Sent_The_Contract_In_Their_Language(
        string language, string subject, string greeting, string body)
    {
        var (service, values, requests) = BuildService();

        await service.SendWorkContractEmailAsync(
            "petra@example.test", "Petra Dvořáková", "ORD-7Q2K", Pdf, "work-contract-ORD-7Q2K.pdf", language, CancellationToken.None);

        Assert.Equal(subject, values["Subject"]);
        Assert.Equal(greeting, values["Greeting"]);
        Assert.StartsWith(body, values["Body"]);
        Assert.DoesNotContain("{", string.Join(" ", values.Values));
        Assert.Contains("work-contract-ORD-7Q2K.pdf", Assert.Single(requests));
    }

    private static Order BuildOrder(PaymentType paymentType)
    {
        var order = Order.Create(
            customerName: "Jana Novakova",
            customerEmail: Recipient,
            customerPhone: "+420777123456",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: paymentType,
            totalPrice: 1234.5m,
            currencyId: "czk",
            paymentStatus: paymentType == PaymentType.Card ? PaymentStatus.Paid : PaymentStatus.Pending,
            userId: "user-1");
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = "czk";
        order.SetCurrency(czk);
        return order;
    }

    /// <summary>The real service with a renderer double that keeps the values it was handed, and a transport that keeps each request body.</summary>
    private static (EmailService Service, Dictionary<string, string?> Values, List<string> Requests) BuildService()
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

        var requests = new List<string>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(new RecordingHandler(requests), disposeHandler: false));

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

        return (service, captured, requests);
    }

    private sealed class RecordingHandler(List<string> requests) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
