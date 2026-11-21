using System.Net;
using System.Text;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// What the platform reads back from a SetupIntent a sheet confirmed: its Customer, the saved-card row its
/// metadata names and the card it saved, which is none until the intent has succeeded; and the card a phone's
/// Plus setup confirmed, which its subscription renews on.
/// </summary>
public class StripeSetupIntentTests
{
    [Fact]
    public async Task A_Succeeded_Setup_Is_Read_With_Its_Customer_Its_Row_And_Its_Card()
    {
        var transport = new RecordingHandler(SetupIntent("succeeded", """{"SavedCardId":"card-row-1"}"""));

        var read = await Client(transport).GetSetupIntentCardAsync("seti_1", CancellationToken.None);

        Assert.Equal(new SetupIntentCard("cus_eur", "card-row-1", new SavedCardDetails("pm_1", "visa", "4242", 9, 2031)), read);
        Assert.Equal((HttpMethod.Get, "/v1/setup_intents/seti_1"), (transport.Requests[0].Method, transport.Requests[0].Path));
    }

    [Fact]
    public async Task A_Setup_That_Has_Not_Succeeded_Is_Read_With_No_Card()
    {
        var transport = new RecordingHandler(SetupIntent("requires_action", "{}"));

        var read = await Client(transport).GetSetupIntentCardAsync("seti_1", CancellationToken.None);

        Assert.Equal(new SetupIntentCard("cus_eur", null, null), read);
    }

    /// <summary>
    /// The phone's Plus sheet confirms the platform's own SetupIntent and the confirm sends no card, so the
    /// subscription takes the card of the newest Plus setup that succeeded on its Customer as its default, and
    /// its after-trial and renewal invoices have a card to charge. A saved-card setup on the same Customer is
    /// not a Plus one.
    /// </summary>
    [Fact]
    public async Task A_Phone_Subscription_Renews_On_The_Card_Its_Plus_Setup_Confirmed()
    {
        var transport = new RecordingHandler(
            SetupIntents(
                ("seti_saved", "succeeded", "pm_saved", """{"SavedCardId":"card-row-1"}"""),
                ("seti_plus_open", "requires_payment_method", null, "{}"),
                ("seti_plus", "succeeded", "pm_plus", "{}"),
                ("seti_plus_old", "succeeded", "pm_plus_old", "{}")),
            Subscription);

        await Client(transport).CreateSubscriptionAsync("cus_eur", "price_plus", "user-1", "PLUS_MONTHLY", 14, "tok-1", CancellationToken.None);

        Assert.Equal((HttpMethod.Get, "/v1/setup_intents"), (transport.Requests[0].Method, transport.Requests[0].Path));
        Assert.Contains("customer=cus_eur", transport.Requests[0].Query);
        var create = transport.Requests[1];
        Assert.Equal((HttpMethod.Post, "/v1/subscriptions"), (create.Method, create.Path));
        Assert.Contains("&default_payment_method=pm_plus&", "&" + create.Body + "&");
    }

    [Fact]
    public async Task A_Subscription_With_No_Succeeded_Plus_Setup_Names_No_Default()
    {
        var transport = new RecordingHandler(
            SetupIntents(("seti_saved", "succeeded", "pm_saved", """{"SavedCardId":"card-row-1"}""")),
            Subscription);

        await Client(transport).CreateSubscriptionAsync("cus_eur", "price_plus", "user-1", "PLUS_MONTHLY", 14, "tok-1", CancellationToken.None);

        Assert.DoesNotContain("&default_payment_method=", "&" + transport.Requests[1].Body);
    }

    private const string Subscription =
        """{"id":"sub_1","object":"subscription","status":"trialing","items":{"object":"list","data":[]}}""";

    private static string SetupIntents(params (string Id, string Status, string? PaymentMethod, string Metadata)[] intents) =>
        """{"object":"list","has_more":false,"url":"/v1/setup_intents","data":["""
        + string.Join(",", intents.Select(i =>
            $$"""{"id":"{{i.Id}}","object":"setup_intent","status":"{{i.Status}}","customer":"cus_eur","payment_method":{{Quoted(i.PaymentMethod)}},"metadata":{{i.Metadata}} }"""))
        + "]}";

    private static string Quoted(string? value) => value is null ? "null" : $"\"{value}\"";

    private static string SetupIntent(string status, string metadata) =>
        $$"""
        {"id":"seti_1","object":"setup_intent","status":"{{status}}","customer":"cus_eur","metadata":{{metadata}},
         "payment_method":{"id":"pm_1","object":"payment_method","type":"card",
                           "card":{"brand":"visa","last4":"4242","exp_month":9,"exp_year":2031} } }
        """;

    private static StripeClient Client(RecordingHandler transport) => new(
        new StubStripeConfig(),
        new StubHttpClientFactory(new HttpClient(transport)),
        NullLogger<StripeClient>.Instance);

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string Query, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : Uri.UnescapeDataString(await request.Content.ReadAsStringAsync(cancellationToken));
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, Uri.UnescapeDataString(request.RequestUri.Query), body));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[Requests.Count - 1], Encoding.UTF8, "application/json"),
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
