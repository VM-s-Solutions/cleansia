using System.Net;
using System.Text;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Integration;

/// <summary>
/// Stripe forgets an idempotency key after about a day, so a refund retried later is matched to the one Stripe
/// already made by the <c>RefundKey</c> metadata both refund calls tag it with. The lookup lists the refunds of
/// the order's PaymentIntent, page by page, and counts only a refund carrying our key as ours, never one that
/// merely has the same amount. An unreachable Stripe throws and never reads as "no refund".
/// </summary>
public class StripeRefundLookupTests
{
    private const string RefundKey = "refund:order-1:cancel";

    [Fact]
    public async Task A_Payment_Intent_Refund_Carries_Its_Key_As_Metadata_Beside_The_Idempotency_Key()
    {
        var transport = new RecordingHandler(Ok(Refund("re_1", 75000, "succeeded", RefundKey)));

        await Client(transport).RefundPaymentIntentAsync("pi_x", 750m, RefundKey, CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Equal((HttpMethod.Post, "/v1/refunds"), (request.Method, request.Path));
        Assert.Contains($"metadata[RefundKey]={RefundKey}", request.Body);
        Assert.Equal(RefundKey, request.IdempotencyKey);
    }

    [Fact]
    public async Task A_Checkout_Session_Refund_Carries_Its_Key_As_Metadata_Beside_The_Idempotency_Key()
    {
        var transport = new RecordingHandler(
            Ok(Session("cs_x", "pi_x")), Ok(Refund("re_1", 75000, "succeeded", RefundKey)));

        await Client(transport).RefundCheckoutSessionAsync("cs_x", 750m, RefundKey, CancellationToken.None);

        var request = transport.Requests[^1];
        Assert.Equal((HttpMethod.Post, "/v1/refunds"), (request.Method, request.Path));
        Assert.Contains("payment_intent=pi_x", request.Body);
        Assert.Contains($"metadata[RefundKey]={RefundKey}", request.Body);
        Assert.Equal(RefundKey, request.IdempotencyKey);
    }

    [Fact]
    public async Task The_Lookup_Lists_The_Refunds_Of_The_Payment_Intent_And_Maps_Minor_Units()
    {
        var transport = new RecordingHandler(Ok(List(hasMore: false, Refund("re_1", 75000, "succeeded", RefundKey))));

        var found = await Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None);

        Assert.Equal(new StripeRefundSnapshot("re_1", 750.00m, Failed: false), found);
        var request = Assert.Single(transport.Requests);
        Assert.Equal((HttpMethod.Get, "/v1/refunds"), (request.Method, request.Path));
        Assert.Contains("payment_intent=pi_x", request.Query);
        Assert.Contains("limit=100", request.Query);
    }

    [Fact]
    public async Task A_Refund_Without_Our_Key_Is_Not_Ours_Even_For_The_Same_Amount()
    {
        var transport = new RecordingHandler(Ok(List(hasMore: false,
            Refund("re_dashboard", 75000, "succeeded", key: null),
            Refund("re_other", 75000, "succeeded", "refund:order-1:admin:full"))));

        var found = await Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None);

        Assert.Null(found);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("canceled")]
    public async Task A_Failed_Or_Canceled_Refund_Is_Reported_Failed(string status)
    {
        var transport = new RecordingHandler(Ok(List(hasMore: false, Refund("re_1", 75000, status, RefundKey))));

        var found = await Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None);

        Assert.Equal(new StripeRefundSnapshot("re_1", 750m, Failed: true), found);
    }

    [Theory]
    [InlineData("succeeded")]
    [InlineData("pending")]
    [InlineData("requires_action")]
    public async Task A_Live_Refund_Is_Preferred_Over_A_Failed_One_Under_The_Same_Key(string live)
    {
        var transport = new RecordingHandler(Ok(List(hasMore: false,
            Refund("re_failed", 75000, "failed", RefundKey),
            Refund("re_live", 75000, live, RefundKey))));

        var found = await Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None);

        Assert.Equal(new StripeRefundSnapshot("re_live", 750m, Failed: false), found);
    }

    [Fact]
    public async Task The_Lookup_Reads_Every_Page()
    {
        var transport = new RecordingHandler(
            Ok(List(hasMore: true, Refund("re_a", 1000, "succeeded", "refund:order-1:admin:partial-1"))),
            Ok(List(hasMore: false, Refund("re_b", 2000, "succeeded", RefundKey))));

        var found = await Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None);

        Assert.Equal("re_b", found?.Id);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Contains("starting_after=re_a", transport.Requests[1].Query);
    }

    [Fact]
    public async Task A_Session_Order_Resolves_Its_Payment_Intent_Through_The_Session_First()
    {
        var transport = new RecordingHandler(
            Ok(Session("cs_x", "pi_session")),
            Ok(List(hasMore: false, Refund("re_1", 75000, "succeeded", RefundKey))));

        var found = await Client(transport).FindRefundAsync("cs_x", null, RefundKey, CancellationToken.None);

        Assert.Equal("re_1", found?.Id);
        Assert.Equal((HttpMethod.Get, "/v1/checkout/sessions/cs_x"), (transport.Requests[0].Method, transport.Requests[0].Path));
        Assert.Contains("payment_intent=pi_session", transport.Requests[1].Query);
    }

    [Fact]
    public async Task A_Session_With_No_Payment_Intent_Has_No_Refund()
    {
        var transport = new RecordingHandler(Ok(Session("cs_x", paymentIntentId: null)));

        var found = await Client(transport).FindRefundAsync("cs_x", null, RefundKey, CancellationToken.None);

        Assert.Null(found);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task A_Server_Error_Throws_And_Never_Reads_As_No_Refund()
    {
        var transport = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                """{"error":{"type":"api_error","message":"Something went wrong"}}""", Encoding.UTF8, "application/json"),
        });

        await Assert.ThrowsAsync<StripeException>(
            () => Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None));
    }

    [Fact]
    public async Task A_Timeout_Throws_And_Never_Reads_As_No_Refund()
    {
        var transport = new RecordingHandler(new HttpRequestException("timed out"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(transport).FindRefundAsync(null, "pi_x", RefundKey, CancellationToken.None));
    }

    private static string Refund(string id, long amount, string status, string? key)
    {
        var metadata = key is null ? "{}" : $$"""{"RefundKey":"{{key}}"}""";
        return $$"""{"id":"{{id}}","object":"refund","amount":{{amount}},"currency":"czk","status":"{{status}}","metadata":{{metadata}}}""";
    }

    private static string List(bool hasMore, params string[] refunds)
    {
        var more = hasMore ? "true" : "false";
        return $$"""{"object":"list","url":"/v1/refunds","has_more":{{more}},"data":[{{string.Join(',', refunds)}}]}""";
    }

    private static string Session(string id, string? paymentIntentId)
    {
        var intent = paymentIntentId is null ? "null" : $"\"{paymentIntentId}\"";
        return $$"""{"id":"{{id}}","object":"checkout.session","payment_intent":{{intent}}}""";
    }

    private static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static StripeClient Client(RecordingHandler transport) => new(
        new StubStripeConfig(),
        new StubHttpClientFactory(new HttpClient(transport)),
        NullLogger<StripeClient>.Instance);

    private sealed class RecordingHandler(params object[] answers) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string Query, string Body, string? IdempotencyKey)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : Uri.UnescapeDataString(await request.Content.ReadAsStringAsync(cancellationToken));
            var key = request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null;
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath,
                Uri.UnescapeDataString(request.RequestUri.Query), body, key));
            return answers[Requests.Count - 1] switch
            {
                Exception ex => throw ex,
                HttpResponseMessage response => response,
                var other => throw new InvalidOperationException($"Unexpected answer {other}"),
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
