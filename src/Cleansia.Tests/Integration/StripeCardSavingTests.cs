using System.Net;
using System.Text;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// Stripe keeps a card only when the customer ticked "save this card": the booking checkout and the
/// PaymentSheet intent ask for <c>setup_future_usage</c> and carry the saved card's id then, and never
/// otherwise. A checkout the customer walked away from is handed back while Stripe still has it open, so
/// resuming a card-saving checkout is not refused for asking with other parameters under the same key.
/// </summary>
public class StripeCardSavingTests
{
    private const string SavedCardId = "card-saving-1";

    [Fact]
    public async Task A_Booking_Checkout_Without_The_Tick_Asks_Stripe_To_Keep_Nothing()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCheckoutSessionAsync(order, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.DoesNotContain("setup_future_usage", request.Body);
        Assert.DoesNotContain("customer=", request.Body);
        Assert.DoesNotContain("SavedCardId", request.Body);
    }

    [Fact]
    public async Task A_Card_Saving_Checkout_Keeps_The_Card_On_The_Customer_And_Names_Its_Row()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCardSavingCheckoutSessionAsync(order, "cus_czk", SavedCardId, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Contains("payment_intent_data[setup_future_usage]=off_session", request.Body);
        Assert.Contains($"payment_intent_data[metadata][SavedCardId]={SavedCardId}", request.Body);
        Assert.Contains($"metadata[SavedCardId]={SavedCardId}", request.Body);
        Assert.Contains($"metadata[OrderId]={order.Id}", request.Body);
        Assert.Contains("customer=cus_czk", request.Body);
        Assert.Equal($"checkout-{order.Id}", request.IdempotencyKey);
    }

    [Fact]
    public async Task A_PaymentSheet_Intent_Without_The_Tick_Asks_Stripe_To_Keep_Nothing()
    {
        var transport = new RecordingHandler();

        await Client(transport).CreatePaymentIntentAsync(
            1500m, "CZK", "cus_czk", "order-sheet", "CL-1", savedCardId: null, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.DoesNotContain("setup_future_usage", request.Body);
        Assert.DoesNotContain("SavedCardId", request.Body);
        Assert.Equal("pi-order-sheet-150000", request.IdempotencyKey);
    }

    /// <summary>
    /// The saving intent differs from the plain one in its parameters, so it is keyed on its own row: under
    /// the plain key Stripe would refuse it as a replay with other parameters.
    /// </summary>
    [Fact]
    public async Task A_PaymentSheet_Intent_With_The_Tick_Keeps_The_Card_And_Is_Keyed_On_Its_Row()
    {
        var transport = new RecordingHandler();

        await Client(transport).CreatePaymentIntentAsync(
            1500m, "CZK", "cus_czk", "order-sheet", "CL-1", SavedCardId, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Contains("setup_future_usage=off_session", request.Body);
        Assert.Contains($"metadata[SavedCardId]={SavedCardId}", request.Body);
        Assert.Contains("customer=cus_czk", request.Body);
        Assert.Equal($"pi-order-sheet-150000-card-{SavedCardId}", request.IdempotencyKey);
    }

    [Fact]
    public async Task Resuming_An_Order_Whose_Checkout_Is_Still_Open_Hands_That_Checkout_Back()
    {
        var transport = new RecordingHandler { SessionStatus = "open" };
        var order = CardOrder();
        order.AssignStripeSessionId("cs_started");

        var session = await Client(transport).CreateCheckoutSessionAsync(order, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/v1/checkout/sessions/cs_started", request.Path);
        Assert.Equal(("cs_started", "https://checkout.stripe.test/cs_started"), (session.Id, session.Url));
    }

    [Fact]
    public async Task Resuming_An_Order_Whose_Checkout_Has_Closed_Asks_Stripe_For_The_Session_Again()
    {
        var transport = new RecordingHandler { SessionStatus = "expired" };
        var order = CardOrder();
        order.AssignStripeSessionId("cs_started");

        await Client(transport).CreateCheckoutSessionAsync(order, CancellationToken.None);

        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Post }, transport.Requests.Select(r => r.Method));
        Assert.Equal($"checkout-{order.Id}", transport.Requests[1].IdempotencyKey);
    }

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
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        order.Id = "order-card-saving";
        order.SetCurrency(Currency.Create("CZK", "Kč", "Czech koruna"));
        return order;
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string Body, string? IdempotencyKey);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public string SessionStatus { get; init; } = "open";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : Uri.UnescapeDataString(await request.Content.ReadAsStringAsync(cancellationToken));
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(new RecordedRequest(
                request.Method,
                path,
                body,
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));

            var json = path.Contains("/payment_intents")
                ? """{"id":"pi_sheet","object":"payment_intent","client_secret":"pi_sheet_secret"}"""
                : request.Method == HttpMethod.Get
                    ? $$"""{"id":"cs_started","object":"checkout.session","status":"{{SessionStatus}}","url":"https://checkout.stripe.test/cs_started"}"""
                    : """{"id":"cs_new","object":"checkout.session","status":"open","url":"https://checkout.stripe.test/cs_new"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
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
