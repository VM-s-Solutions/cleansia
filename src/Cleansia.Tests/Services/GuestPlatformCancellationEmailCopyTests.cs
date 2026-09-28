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
/// When the platform cancels a guest's booking, the e-mail is the only word the guest gets. It says why —
/// no cleaner, an unfinished payment, or simply that we cancelled — and what happened to the money, which
/// is only ever what actually happened: a refund that went through, one attempted and still being processed,
/// or nothing taken at all. A booking the guest cancelled themselves keeps its own wording.
/// </summary>
public sealed class GuestPlatformCancellationEmailCopyTests
{
    private const string Recipient = "guest@example.com";

    [Theory]
    [InlineData("en", "No cleaner was available for this booking, so it was cancelled.")]
    [InlineData("cs", "Pro tuto rezervaci nebyl k dispozici žádný uklízeč, a proto byla zrušena.")]
    [InlineData("sk", "Pre túto rezerváciu nebol k dispozícii žiadny upratovač, a preto bola zrušená.")]
    [InlineData("uk", "Для цього бронювання не знайшлося вільного прибиральника, тому його скасовано.")]
    [InlineData("ru", "Для этого бронирования не нашлось свободного уборщика, поэтому оно отменено.")]
    public async Task No_Cleaner_Found_Says_So_In_The_Guests_Language(string language, string reason)
    {
        var order = PaidCardOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, order.TotalPrice, OrderCancellationReasons.NoCleanerAvailable);

        var message = await StatusMessageAsync(order, language, refundedAmount: 1000m);

        Assert.Contains(reason, message);
    }

    [Theory]
    [InlineData("en", "The payment wasn't completed, so this booking was released.", "Nothing was charged.")]
    [InlineData("cs", "Platba nebyla dokončena, a proto byla tato rezervace uvolněna.", "Nic vám nebylo účtováno.")]
    [InlineData("sk", "Platba nebola dokončená, a preto bola táto rezervácia uvoľnená.", "Nič vám nebolo účtované.")]
    [InlineData("uk", "Оплату не було завершено, тому це бронювання було скасовано.", "З вас нічого не стягнуто.")]
    [InlineData("ru", "Оплата не была завершена, поэтому это бронирование было отменено.", "С вас ничего не списано.")]
    public async Task An_Unfinished_Payment_Says_So_And_That_Nothing_Was_Charged(
        string language, string reason, string nothingCharged)
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.Failed);
        order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, 0m, OrderCancellationReasons.PaymentNotCompleted);

        var message = await StatusMessageAsync(order, language, refundedAmount: null);

        Assert.Contains(reason, message);
        Assert.Contains(nothingCharged, message);
    }

    [Theory]
    [InlineData("en", "We had to cancel this booking.", "Your refund is being processed.")]
    [InlineData("cs", "Tuto rezervaci jsme museli zrušit.", "Vrácení peněz zpracováváme.")]
    [InlineData("sk", "Túto rezerváciu sme museli zrušiť.", "Vrátenie peňazí spracúvame.")]
    [InlineData("uk", "Нам довелося скасувати це бронювання.", "Повернення коштів обробляється.")]
    [InlineData("ru", "Нам пришлось отменить это бронирование.", "Возврат средств обрабатывается.")]
    public async Task A_Paid_Booking_We_Cancelled_Whose_Refund_Did_Not_Go_Through_Is_Being_Processed_Never_Refunded(
        string language, string reason, string processing)
    {
        var order = PaidCardOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, 0m, order.TotalPrice, "Double booking on our side");

        var message = await StatusMessageAsync(order, language, refundedAmount: null);

        Assert.Contains(reason, message);
        Assert.Contains(processing, message);
        Assert.DoesNotContain("Double booking", message);
    }

    [Fact]
    public async Task A_Refund_That_Went_Through_States_What_Came_Back()
    {
        var order = PaidCardOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, order.TotalPrice, OrderCancellationReasons.CompanyWindDown);

        var message = await StatusMessageAsync(order, "en", refundedAmount: 1000m);

        Assert.Contains("We had to cancel this booking.", message);
        Assert.Contains($"Refund issued: €{1000m:N2}.", message);
        Assert.DoesNotContain("being processed", message);
        Assert.DoesNotContain("Nothing was charged", message);
    }

    [Fact]
    public async Task A_Cash_Booking_We_Cancelled_Took_Nothing()
    {
        var order = NewOrder(PaymentType.Cash, PaymentStatus.Pending);
        order.Cancel(DateTime.UtcNow, CancelledBy.System, 0m, order.TotalPrice, OrderCancellationReasons.NoCleanerAvailable);

        var message = await StatusMessageAsync(order, "en", refundedAmount: null);

        Assert.Contains("Nothing was charged.", message);
        Assert.DoesNotContain("Refund", message);
        Assert.DoesNotContain("being processed", message);
    }

    /// <summary>
    /// A platform cancellation refunds only a paid card charge it can reach. An order already refunded, under a
    /// bank dispute, or with no charge to refund against had no refund attempted, so the guest is promised none.
    /// </summary>
    [Theory]
    [InlineData(PaymentStatus.Refunded, true)]
    [InlineData(PaymentStatus.PartiallyRefunded, true)]
    [InlineData(PaymentStatus.Disputed, true)]
    [InlineData(PaymentStatus.Paid, false)]
    public async Task A_Booking_We_Cancelled_Without_Attempting_A_Refund_Promises_None(
        PaymentStatus paymentStatus, bool hasChargeSurface)
    {
        var order = NewOrder(PaymentType.Card, paymentStatus);
        if (hasChargeSurface)
        {
            order.AssignStripePaymentIntentId("pi_guest_platform_copy");
        }
        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, 0m, order.TotalPrice, "Double booking on our side");

        var message = await StatusMessageAsync(order, "en", refundedAmount: null);

        Assert.Contains("We had to cancel this booking.", message);
        Assert.DoesNotContain("being processed", message);
        Assert.DoesNotContain("Refund issued", message);
        Assert.DoesNotContain("Nothing was charged", message);
    }

    [Fact]
    public async Task A_Booking_The_Guest_Cancelled_Keeps_Its_Own_Wording()
    {
        var order = PaidCardOrder();
        order.Cancel(DateTime.UtcNow, CancelledBy.Customer, 1m, 0m, "Changed my plans");

        var message = await StatusMessageAsync(order, "en", refundedAmount: null);

        Assert.Equal("Your order has been cancelled.", message);
    }

    private static Order PaidCardOrder()
    {
        var order = NewOrder(PaymentType.Card, PaymentStatus.Paid);
        order.AssignStripePaymentIntentId("pi_guest_platform_copy");
        return order;
    }

    private static Order NewOrder(PaymentType paymentType, PaymentStatus paymentStatus)
    {
        var order = Order.Create(
            customerName: "Guest Customer",
            customerEmail: Recipient,
            customerPhone: "+420000000000",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: paymentType,
            totalPrice: 1000m,
            currencyId: "eur",
            paymentStatus: paymentStatus);
        var euro = Currency.Create("EUR", "€", "Euro");
        euro.Id = "eur";
        order.SetCurrency(euro);
        return order;
    }

    private static async Task<string> StatusMessageAsync(Order order, string language, decimal? refundedAmount)
    {
        var (service, values) = BuildService();
        await service.SendOrderStatusUpdateEmailAsync(Recipient, order, "Cancelled", language,
            CancellationToken.None, refundedAmount, guestAccessToken: "token");
        return values["StatusMessage"]!;
    }

    private static (EmailService Service, Dictionary<string, string?> Values) BuildService()
    {
        var config = new Mock<ISendGridConfig>();
        config.SetupGet(c => c.ApiKey).Returns("SG.test");
        config.SetupGet(c => c.AddressFrom).Returns("noreply@example.test");
        config.SetupGet(c => c.ClientDomainUrl).Returns("https://app.test");

        var translationRepository = new Mock<IEmailTemplateTranslationRepository>();
        translationRepository
            .Setup(r => r.GetTranslationsByTypeAndLanguageAsync(
                EmailType.OrderStatusUpdate, It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
