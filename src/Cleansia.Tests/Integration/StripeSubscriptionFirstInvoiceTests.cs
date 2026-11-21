using System.Net;
using System.Text;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// What the phone's Plus subscribe asks Stripe, and how it reads the first invoice's outcome. With no trial
/// Stripe is asked to charge the first invoice at once with the subscription's default card
/// (<c>allow_incomplete</c>); a trial keeps <c>default_incomplete</c>, whose first invoice is free. Both carry the
/// UserId and MembershipPlanCode metadata the subscription webhooks provision from. A subscription that is not
/// live is read again, because Stripe replays a retried create's first answer, and its invoice's PaymentIntent
/// says whether the card needs 3-D Secure, was declined, or has paid.
/// </summary>
public class StripeSubscriptionFirstInvoiceTests
{
    [Fact]
    public async Task A_subscribe_with_no_trial_is_charged_at_once_and_tagged_with_its_owner_and_plan()
    {
        var transport = new StripeFake { Created = Subscription("active") };

        var result = await Client(transport).CreateSubscriptionAsync(
            "cus_czk", "price_plus", "user-1", "PLUS_MONTHLY", 0, "tok-1", CancellationToken.None);

        Assert.Equal(("sub_1", "active", null, null), (result.SubscriptionId, result.Status, result.PaymentIntentClientSecret, result.DeclineCode));
        var create = Assert.Single(transport.Requests, r => r.Method == HttpMethod.Post);
        Assert.Contains("payment_behavior=allow_incomplete", create.Body);
        Assert.Contains("metadata[UserId]=user-1", create.Body);
        Assert.Contains("metadata[MembershipPlanCode]=PLUS_MONTHLY", create.Body);
        Assert.DoesNotContain("trial_period_days", create.Body);
        Assert.DoesNotContain(transport.Requests, r => r.Path.StartsWith("/v1/subscriptions/") || r.Path == "/v1/invoice_payments");
    }

    [Fact]
    public async Task A_trial_subscribe_keeps_default_incomplete_and_carries_the_same_metadata()
    {
        var transport = new StripeFake { Created = Subscription("trialing") };

        var result = await Client(transport).CreateSubscriptionAsync(
            "cus_czk", "price_plus", "user-1", "PLUS_MONTHLY", 14, "tok-1", CancellationToken.None);

        Assert.Equal("trialing", result.Status);
        var create = Assert.Single(transport.Requests, r => r.Method == HttpMethod.Post);
        Assert.Contains("payment_behavior=default_incomplete", create.Body);
        Assert.Contains("trial_period_days=14", create.Body);
        Assert.Contains("metadata[UserId]=user-1", create.Body);
        Assert.Contains("metadata[MembershipPlanCode]=PLUS_MONTHLY", create.Body);
    }

    [Fact]
    public async Task A_first_invoice_that_needs_3ds_returns_its_payment_intent_secret()
    {
        var transport = new StripeFake
        {
            Created = Subscription("incomplete"),
            Current = Subscription("incomplete"),
            InvoicePayments = Payments("""{"id":"pi_1","object":"payment_intent","status":"requires_action","client_secret":"pi_1_secret_2"}"""),
        };

        var result = await Client(transport).CreateSubscriptionAsync(
            "cus_czk", "price_plus", "user-1", "PLUS_MONTHLY", 0, "tok-1", CancellationToken.None);

        Assert.Equal(("incomplete", "pi_1_secret_2", null), (result.Status, result.PaymentIntentClientSecret, result.DeclineCode));
        var paymentsRead = Assert.Single(transport.Requests, r => r.Path == "/v1/invoice_payments");
        Assert.Contains("invoice=in_1", paymentsRead.Query);
    }

    [Theory]
    [InlineData("""{"code":"card_declined","decline_code":"insufficient_funds","type":"card_error"}""", "insufficient_funds")]
    [InlineData("""{"code":"payment_intent_authentication_failure","type":"invalid_request_error"}""", "payment_intent_authentication_failure")]
    public async Task A_declined_first_invoice_returns_the_cards_reason_and_no_secret(string lastPaymentError, string expected)
    {
        var transport = new StripeFake
        {
            Created = Subscription("incomplete"),
            Current = Subscription("incomplete"),
            InvoicePayments = Payments($$"""{"id":"pi_1","object":"payment_intent","status":"requires_payment_method","client_secret":"pi_1_secret_2","last_payment_error":{{lastPaymentError}} }"""),
        };

        var result = await Client(transport).CreateSubscriptionAsync(
            "cus_czk", "price_plus", "user-1", "PLUS_MONTHLY", 0, "tok-1", CancellationToken.None);

        Assert.Equal(("incomplete", null, expected), (result.Status, result.PaymentIntentClientSecret, result.DeclineCode));
    }

    [Fact]
    public async Task A_replayed_create_is_read_again_so_a_payment_completed_since_counts()
    {
        var transport = new StripeFake { Created = Subscription("incomplete"), Current = Subscription("active") };

        var result = await Client(transport).CreateSubscriptionAsync(
            "cus_czk", "price_plus", "user-1", "PLUS_MONTHLY", 0, "tok-1", CancellationToken.None);

        Assert.Equal("active", result.Status);
        Assert.Contains(transport.Requests, r => r.Method == HttpMethod.Get && r.Path == "/v1/subscriptions/sub_1");
        Assert.DoesNotContain(transport.Requests, r => r.Path == "/v1/invoice_payments");
    }

    [Fact]
    public async Task A_first_invoice_whose_payment_succeeded_counts_as_paid_before_stripe_marks_the_subscription_active()
    {
        var transport = new StripeFake
        {
            Created = Subscription("incomplete"),
            Current = Subscription("incomplete"),
            InvoicePayments = Payments("""{"id":"pi_1","object":"payment_intent","status":"succeeded","client_secret":"pi_1_secret_2"}"""),
        };

        var result = await Client(transport).CreateSubscriptionAsync(
            "cus_czk", "price_plus", "user-1", "PLUS_MONTHLY", 0, "tok-1", CancellationToken.None);

        Assert.Equal(("active", null, null), (result.Status, result.PaymentIntentClientSecret, result.DeclineCode));
    }

    private static string Subscription(string status) =>
        $$"""
        {"id":"sub_1","object":"subscription","status":"{{status}}","latest_invoice":"in_1",
         "items":{"object":"list","data":[{"id":"si_1","object":"subscription_item","current_period_start":1789516800,"current_period_end":1792108800}]} }
        """;

    private static string Payments(string paymentIntent) =>
        $$"""
        {"object":"list","has_more":false,"url":"/v1/invoice_payments","data":[
          {"id":"inpay_1","object":"invoice_payment","is_default":true,"status":"open","invoice":"in_1",
           "payment":{"type":"payment_intent","payment_intent":{{paymentIntent}} } }]}
        """;

    private static StripeClient Client(StripeFake transport) => new(
        new StubStripeConfig(),
        new StubHttpClientFactory(new HttpClient(transport)),
        NullLogger<StripeClient>.Instance);

    private sealed class StripeFake : HttpMessageHandler
    {
        public required string Created { get; init; }

        public string? Current { get; init; }

        public string? InvoicePayments { get; init; }

        public List<(HttpMethod Method, string Path, string Query, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : Uri.UnescapeDataString(await request.Content.ReadAsStringAsync(cancellationToken));
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method, path, Uri.UnescapeDataString(request.RequestUri.Query), body));
            var json = path switch
            {
                "/v1/setup_intents" => """{"object":"list","data":[],"has_more":false,"url":"/v1/setup_intents"}""",
                "/v1/subscriptions" => Created,
                "/v1/invoice_payments" => InvoicePayments ?? throw new InvalidOperationException("invoice payments read"),
                _ => Current ?? throw new InvalidOperationException($"unexpected {request.Method} {path}"),
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
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
