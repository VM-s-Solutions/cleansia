using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// A session given its own expiry sends it to Stripe and keys the request on it, so asking again with the
/// same expiry replays the same session and a later expiry opens the next one. The booking checkout keeps
/// Stripe's default lifetime and its one key per order.
/// </summary>
public class StripeCheckoutSessionExpiryTests
{
    private static readonly DateTime ExpiresAt = new(2026, 10, 3, 7, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task A_Session_With_An_Expiry_Sends_It_And_Is_Keyed_On_It()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCheckoutSessionAsync(order, ExpiresAt, CancellationToken.None);

        var unix = new DateTimeOffset(ExpiresAt).ToUnixTimeSeconds();
        Assert.Contains($"expires_at={unix}", transport.Body);
        Assert.Equal($"checkout-{order.Id}-{unix}", transport.IdempotencyKey);
    }

    /// <summary>
    /// The booking cancel page offers Resume, which asks without the expiry. A customer backing out of an
    /// expiring session goes back to the order's own page instead, where confirming again replays it.
    /// </summary>
    [Fact]
    public async Task A_Session_With_An_Expiry_Cancels_Back_To_The_Orders_Page()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCheckoutSessionAsync(order, ExpiresAt, CancellationToken.None);

        Assert.Equal($"https://unit.test/orders/{order.Id}", CancelUrl(transport.Body));
    }

    [Fact]
    public async Task A_Booking_Checkout_Keeps_Stripes_Lifetime_And_Its_Key()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCheckoutSessionAsync(order, CancellationToken.None);

        Assert.DoesNotContain("expires_at", transport.Body);
        Assert.Equal($"checkout-{order.Id}", transport.IdempotencyKey);
        Assert.Equal($"https://unit.test/cancel?orderId={order.Id}", CancelUrl(transport.Body));
    }

    private static string CancelUrl(string body) =>
        Regex.Match(body, "(?:^|&)cancel_url=(?<url>[^&]*)").Groups["url"].Value;

    private static StripeClient Client(RecordingHandler transport) => new(
        new StubStripeConfig(),
        new StubHttpClientFactory(new HttpClient(transport)),
        NullLogger<StripeClient>.Instance);

    private static Order CardOrder()
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: ExpiresAt.AddHours(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        order.Id = "order-expiring-checkout";
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        return order;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        public string? IdempotencyKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = Uri.UnescapeDataString(await request.Content!.ReadAsStringAsync(cancellationToken));
            IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":"cs_test_expiring","object":"checkout.session","url":"https://checkout.stripe.test/cs_test_expiring"}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubStripeConfig : IStripeConfig
    {
        public bool Enabled { get; set; } = true;
        public string SecretKey { get; set; } = "sk_test_unit";
        public string PublishableKey { get; set; } = "pk_test_unit";
        public string WebhookSecret { get; set; } = "whsec_unit";
        public string SuccessUrlBase { get; set; } = "https://unit.test/success";
        public string CancelUrlBase { get; set; } = "https://unit.test/cancel";
    }
}
