using System.Globalization;
using System.Net;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The cleaning time is stored in UTC. A Czech customer booked 10:00 Prague, so their e-mail says 10:00 —
/// read on the market's clock, the way the receipt reads it — written the way their language writes a
/// date, never the way the server's culture does.
/// </summary>
public sealed class EmailServiceLocalTimeTests
{
    private const string Recipient = "customer@example.com";
    private const string CzechiaId = "country-cz-email-time";

    [Theory]
    [InlineData(7, 8)]
    [InlineData(1, 9)]
    public async Task The_Status_Email_States_The_Cleaning_Time_On_The_Markets_Clock(int month, int utcHour)
    {
        var (service, values) = BuildService(EmailType.OrderStatusUpdate, "Europe/Prague");
        var order = BuildOrder(new DateTime(2026, month, 15, utcHour, 0, 0, DateTimeKind.Utc));

        await InForeignServerCulture(() =>
            service.SendOrderStatusUpdateEmailAsync(Recipient, order, "confirmed", "cs", CancellationToken.None));

        Assert.Equal(new DateTime(2026, month, 15, 10, 0, 0).ToString("g", CultureInfo.GetCultureInfo("cs")),
            values["CleaningDate"]);
    }

    [Fact]
    public async Task The_Status_Email_Writes_The_Time_In_The_Emails_Language()
    {
        var (service, values) = BuildService(EmailType.OrderStatusUpdate, "Europe/Prague");
        var order = BuildOrder(new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc));
        var local = new DateTime(2026, 7, 15, 10, 0, 0);

        await InForeignServerCulture(() =>
            service.SendOrderStatusUpdateEmailAsync(Recipient, order, "confirmed", "en", CancellationToken.None));
        var english = values["CleaningDate"];
        await InForeignServerCulture(() =>
            service.SendOrderStatusUpdateEmailAsync(Recipient, order, "confirmed", "sk", CancellationToken.None));
        var slovak = values["CleaningDate"];

        Assert.Equal(local.ToString("g", CultureInfo.GetCultureInfo("en")), english);
        Assert.Equal(local.ToString("g", CultureInfo.GetCultureInfo("sk")), slovak);
        Assert.NotEqual(english, slovak);
    }

    /// <summary>23:30Z on 31 March is already 1 April in Prague.</summary>
    [Fact]
    public async Task The_Receipt_Email_Dates_The_Order_On_The_Markets_Calendar_In_The_Emails_Language()
    {
        var (service, values) = BuildService(EmailType.OrderReceipt, "Europe/Prague");
        var order = BuildOrder(new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc));
        order.Created("seed", new DateTimeOffset(2026, 3, 31, 23, 30, 0, TimeSpan.Zero));

        await InForeignServerCulture(() =>
            service.SendOrderReceiptEmailAsync(Recipient, order, languageCode: "cs", ct: CancellationToken.None));

        Assert.Equal(new DateTime(2026, 4, 1).ToString("d", CultureInfo.GetCultureInfo("cs")), values["OrderDate"]);
    }

    private static async Task InForeignServerCulture(Func<Task> send)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
        try
        {
            await send();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static Order BuildOrder(DateTime cleaningDateTime) =>
        Order.Create(
            customerName: "Test Customer",
            customerEmail: Recipient,
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", CzechiaId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Pending);

    private static (EmailService Service, Dictionary<string, string?> Values) BuildService(
        EmailType emailType, string timeZoneId)
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                emailType, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, string>());

        var countries = new Mock<ICountryConfigurationRepository>();
        countries
            .Setup(r => r.GetByCountryIdAsync(CzechiaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CountryConfiguration.Create(CzechiaId, "CZK", "cs", 0.21m, timeZoneId: timeZoneId));

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
            renderer.Object,
            countries.Object);

        return (service, captured);
    }

    private sealed class AcceptingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
    }
}
