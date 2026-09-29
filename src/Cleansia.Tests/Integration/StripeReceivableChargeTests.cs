using System.Net;
using System.Text;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// The two Stripe calls a receivable makes. The off-session charge confirms a PaymentIntent at creation on
/// the saved card with the customer absent, keyed on the receivable's attempt. The pay link is a
/// payment-mode Checkout Session that names the receivable on the session and names no order, so the
/// webhook's order path never mistakes the fee for the booking's sale, and it returns to the order's page.
/// </summary>
public class StripeReceivableChargeTests
{
    [Fact]
    public async Task The_Off_Session_Charge_Confirms_At_Creation_On_The_Saved_Card_With_The_Customer_Absent()
    {
        var transport = new RecordingHandler("""{"id":"pi_test_off","object":"payment_intent","status":"succeeded"}""");

        var paymentIntentId = await Client(transport).ChargeReceivableOffSessionAsync(
            "rcv-1", 375m, "CZK", "cus_owing", "pm_owing", 2, CancellationToken.None);

        Assert.Equal("pi_test_off", paymentIntentId);
        Assert.Contains("off_session=true", transport.Body);
        Assert.Contains("confirm=true", transport.Body);
        Assert.Contains("customer=cus_owing", transport.Body);
        Assert.Contains("payment_method=pm_owing", transport.Body);
        Assert.Contains("amount=37500", transport.Body);
        Assert.Contains("currency=czk", transport.Body);
        Assert.Contains("metadata[ReceivableId]=rcv-1", transport.Body);
        Assert.DoesNotContain("OrderId", transport.Body);
        Assert.Equal("receivable-charge-rcv-1-2", transport.IdempotencyKey);
    }

    [Fact]
    public async Task The_Pay_Link_Names_The_Receivable_On_The_Session_Only_And_Returns_To_The_Order()
    {
        var transport = new RecordingHandler(
            """{"id":"cs_test_link","object":"checkout.session","url":"https://checkout.stripe.test/cs_test_link"}""");

        var link = await Client(transport).CreateReceivableCheckoutSessionAsync(
            "rcv-1", "order-1", "ORD-12345678", 375m, "CZK", CancellationToken.None);

        Assert.Equal(("cs_test_link", "https://checkout.stripe.test/cs_test_link"), (link.Id, link.Url));
        Assert.Contains("mode=payment", transport.Body);
        Assert.Contains("metadata[ReceivableId]=rcv-1", transport.Body);
        Assert.DoesNotContain("payment_intent_data", transport.Body);
        Assert.DoesNotContain("OrderId", transport.Body);
        Assert.Contains("line_items[0][price_data][unit_amount]=37500", transport.Body);
        Assert.Contains("success_url=https://unit.test/orders/order-1", transport.Body);
        Assert.Equal("receivable-checkout-rcv-1", transport.IdempotencyKey);
    }

    private static StripeClient Client(RecordingHandler transport) => new(
        new StubStripeConfig(),
        new StubHttpClientFactory(new HttpClient(transport)),
        NullLogger<StripeClient>.Instance);

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public string Body { get; private set; } = string.Empty;

        public string? IdempotencyKey { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = Uri.UnescapeDataString(await request.Content!.ReadAsStringAsync(cancellationToken));
            IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
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
