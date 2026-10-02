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
/// resuming a card-saving checkout is not refused for asking with other parameters under the same key. An
/// order's PaymentSheet intent is handed back the same way while it can still be paid as asked, and a
/// replacement is keyed on the intent it replaces, which is cancelled as a duplicate. A recurring occurrence's
/// expiring checkout follows the tick too: the open one is handed back while asked for as it was opened, and
/// otherwise expired before a replacement keyed on it opens.
/// </summary>
public class StripeCardSavingTests
{
    private const string SavedCardId = "card-saving-1";

    private static readonly DateTime ExpiresAt = new(2026, 10, 3, 7, 0, 0, DateTimeKind.Utc);

    private static readonly long ExpiresAtUnix = new DateTimeOffset(ExpiresAt).ToUnixTimeSeconds();

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
            1500m, "CZK", "cus_czk", "order-sheet", "CL-1", savedCardId: null, currentPaymentIntentId: null, CancellationToken.None);

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
            1500m, "CZK", "cus_czk", "order-sheet", "CL-1", SavedCardId, currentPaymentIntentId: null, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Contains("setup_future_usage=off_session", request.Body);
        Assert.Contains($"metadata[SavedCardId]={SavedCardId}", request.Body);
        Assert.Contains("customer=cus_czk", request.Body);
        Assert.Equal($"pi-order-sheet-150000-card-{SavedCardId}", request.IdempotencyKey);
    }

    /// <summary>
    /// The sheet re-opened with the tick still on is handed the intent the order records, with its own saved
    /// card, and Stripe is asked to create nothing.
    /// </summary>
    [Fact]
    public async Task Reopening_The_Sheet_Hands_Back_The_Intent_The_Order_Records()
    {
        var transport = new RecordingHandler { CurrentIntent = OpenIntent("requires_payment_method", savesCard: true) };

        var intent = await Client(transport).CreatePaymentIntentAsync(
            1500m, "CZK", "cus_czk", "order-sheet", "CL-1", "card-saving-2", "pi_open", CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/v1/payment_intents/pi_open", request.Path);
        Assert.Equal(("pi_open", "pi_open_secret"), (intent.Id, intent.ClientSecret));
    }

    /// <summary>
    /// An intent that can no longer be paid as asked is replaced, keyed on the one it replaces: unticking after
    /// a ticked open never replays the first plain intent, which was cancelled when the tick went on.
    /// </summary>
    [Theory]
    [InlineData("requires_payment_method", 1500, null, "pi-order-sheet-150000-after-pi_open")]
    [InlineData("requires_payment_method", 1600, SavedCardId, $"pi-order-sheet-160000-after-pi_open-card-{SavedCardId}")]
    [InlineData("canceled", 1500, SavedCardId, $"pi-order-sheet-150000-after-pi_open-card-{SavedCardId}")]
    public async Task An_Intent_That_Cannot_Be_Paid_As_Asked_Is_Replaced_Under_A_Key_Of_Its_Own(
        string status, int amount, string? savedCardId, string expectedKey)
    {
        var transport = new RecordingHandler { CurrentIntent = OpenIntent(status, savesCard: true) };

        var intent = await Client(transport).CreatePaymentIntentAsync(
            amount, "CZK", "cus_czk", "order-sheet", "CL-1", savedCardId, "pi_open", CancellationToken.None);

        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Post }, transport.Requests.Select(r => r.Method));
        Assert.Equal(expectedKey, transport.Requests[1].IdempotencyKey);
        Assert.Equal(savedCardId is not null, transport.Requests[1].Body.Contains("SavedCardId"));
        Assert.Equal("pi_sheet", intent.Id);
    }

    [Fact]
    public async Task A_Replaced_Intent_Is_Cancelled_As_A_Duplicate()
    {
        var transport = new RecordingHandler();

        await Client(transport).CancelReplacedPaymentIntentAsync("pi_open", CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/v1/payment_intents/pi_open/cancel", request.Path);
        Assert.Contains("cancellation_reason=duplicate", request.Body);
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

    [Fact]
    public async Task An_Expiring_Checkout_Without_The_Tick_Asks_Stripe_To_Keep_Nothing()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCheckoutSessionAsync(order, ExpiresAt, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.DoesNotContain("setup_future_usage", request.Body);
        Assert.DoesNotContain("customer=", request.Body);
        Assert.DoesNotContain("SavedCardId", request.Body);
    }

    [Fact]
    public async Task An_Expiring_Card_Saving_Checkout_Keeps_The_Card_On_The_Customer_And_Names_Its_Row()
    {
        var transport = new RecordingHandler();
        var order = CardOrder();

        await Client(transport).CreateCardSavingCheckoutSessionAsync(order, ExpiresAt, "cus_czk", SavedCardId, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Contains("payment_intent_data[setup_future_usage]=off_session", request.Body);
        Assert.Contains($"payment_intent_data[metadata][SavedCardId]={SavedCardId}", request.Body);
        Assert.Contains($"metadata[SavedCardId]={SavedCardId}", request.Body);
        Assert.Contains("customer=cus_czk", request.Body);
        Assert.Contains($"expires_at={ExpiresAtUnix}", request.Body);
        Assert.Contains($"cancel_url=https://unit.test/orders/{order.Id}", request.Body);
        Assert.Equal($"checkout-{order.Id}-{ExpiresAtUnix}", request.IdempotencyKey);
    }

    /// <summary>
    /// Confirming the occurrence again with the tick unchanged gets the session the order records back, and
    /// Stripe is asked to create nothing, so the customer never holds two open sessions for one order.
    /// </summary>
    [Fact]
    public async Task An_Open_Expiring_Checkout_Asked_For_As_Opened_Is_Handed_Back()
    {
        var transport = new RecordingHandler { SessionStatus = "open", SessionSavesCard = true };
        var order = CardOrder();
        order.AssignStripeSessionId("cs_started");

        var session = await Client(transport).CreateCardSavingCheckoutSessionAsync(
            order, ExpiresAt, "cus_czk", "card-saving-2", CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.EndsWith("/v1/checkout/sessions/cs_started", request.Path);
        Assert.Equal("cs_started", session.Id);
    }

    /// <summary>
    /// A changed tick or amount closes the open session before its replacement opens, and the replacement is
    /// keyed on the session it replaces, since the stride's own key already answered with other parameters.
    /// </summary>
    [Theory]
    [InlineData(true, 100000, false)]
    [InlineData(false, 100000, true)]
    [InlineData(true, 90000, true)]
    public async Task An_Open_Expiring_Checkout_Asked_For_Otherwise_Is_Expired_And_Replaced(
        bool currentSavesCard, long currentAmount, bool saveCard)
    {
        var transport = new RecordingHandler
        {
            SessionStatus = "open",
            SessionSavesCard = currentSavesCard,
            SessionAmountTotal = currentAmount,
        };
        var order = CardOrder();
        order.AssignStripeSessionId("cs_started");
        var client = Client(transport);

        var session = saveCard
            ? await client.CreateCardSavingCheckoutSessionAsync(order, ExpiresAt, "cus_czk", SavedCardId, CancellationToken.None)
            : await client.CreateCheckoutSessionAsync(order, ExpiresAt, CancellationToken.None);

        Assert.Equal(
            new[] { (HttpMethod.Get, "/v1/checkout/sessions/cs_started"), (HttpMethod.Post, "/v1/checkout/sessions/cs_started/expire"), (HttpMethod.Post, "/v1/checkout/sessions") },
            transport.Requests.Select(r => (r.Method, r.Path)));
        Assert.Equal($"checkout-{order.Id}-{ExpiresAtUnix}-after-cs_started", transport.Requests[2].IdempotencyKey);
        Assert.Equal(saveCard, transport.Requests[2].Body.Contains("payment_intent_data[setup_future_usage]=off_session"));
        Assert.Equal("cs_new", session.Id);
    }

    [Fact]
    public async Task A_Closed_Expiring_Checkout_Is_Asked_Of_Stripe_Again_Under_The_Strides_Key()
    {
        var transport = new RecordingHandler { SessionStatus = "expired", SessionSavesCard = true };
        var order = CardOrder();
        order.AssignStripeSessionId("cs_started");

        await Client(transport).CreateCheckoutSessionAsync(order, ExpiresAt, CancellationToken.None);

        Assert.Equal(new[] { HttpMethod.Get, HttpMethod.Post }, transport.Requests.Select(r => r.Method));
        Assert.Equal("/v1/checkout/sessions", transport.Requests[1].Path);
        Assert.Equal($"checkout-{order.Id}-{ExpiresAtUnix}", transport.Requests[1].IdempotencyKey);
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

    private static string OpenIntent(string status, bool savesCard)
    {
        var metadata = savesCard
            ? $$"""{"OrderId":"order-sheet","SavedCardId":"{{SavedCardId}}"}"""
            : """{"OrderId":"order-sheet"}""";
        return $$"""
            {"id":"pi_open","object":"payment_intent","status":"{{status}}","amount":150000,"currency":"czk",
             "customer":"cus_czk","client_secret":"pi_open_secret","metadata":{{metadata}} }
            """;
    }

    private sealed record RecordedRequest(HttpMethod Method, string Path, string Body, string? IdempotencyKey);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public string SessionStatus { get; init; } = "open";

        public bool SessionSavesCard { get; init; }

        public long SessionAmountTotal { get; init; } = 100000;

        public string? CurrentIntent { get; init; }

        private string SessionMetadata => SessionSavesCard
            ? $$"""{"OrderId":"order-card-saving","SavedCardId":"{{SavedCardId}}"}"""
            : """{"OrderId":"order-card-saving"}""";

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
                ? request.Method == HttpMethod.Get && CurrentIntent is not null
                    ? CurrentIntent
                    : """{"id":"pi_sheet","object":"payment_intent","client_secret":"pi_sheet_secret"}"""
                : request.Method == HttpMethod.Get
                    ? $$"""{"id":"cs_started","object":"checkout.session","status":"{{SessionStatus}}","amount_total":{{SessionAmountTotal}},"metadata":{{SessionMetadata}},"url":"https://checkout.stripe.test/cs_started"}"""
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
