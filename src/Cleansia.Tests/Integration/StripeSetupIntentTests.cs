using System.Net;
using System.Text;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// What the platform reads back from a SetupIntent a sheet confirmed: its Customer, the saved-card row its
/// metadata names and the card it saved, which is none until the intent has succeeded.
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
