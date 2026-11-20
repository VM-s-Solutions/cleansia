using System.Net;
using System.Text;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// A removed saved card comes off its Stripe Customer, so no later sheet on that Customer offers it again.
/// The Customer is the one per currency that Cleansia Plus also bills, so a card a live subscription or the
/// Customer's invoices pay with stays on it. A card already off the Customer is left alone, so a removal
/// retried after its detach changes nothing at Stripe.
/// </summary>
public class StripeSavedCardDetachTests
{
    private const string CustomerId = "cus_eur_card";
    private const string PaymentMethodId = "pm_card_1";

    private const string NoSubscriptions = """{"object":"list","data":[],"has_more":false,"url":"/v1/subscriptions"}""";

    [Fact]
    public async Task A_Card_Nothing_Else_Pays_With_Is_Detached_From_The_Customer()
    {
        var transport = new RecordingHandler(
            PaymentMethod(CustomerId), Customer(defaultPaymentMethod: null), NoSubscriptions, PaymentMethod(customer: null));

        await Client(transport).DetachSavedCardAsync(CustomerId, PaymentMethodId, CancellationToken.None);

        var detach = transport.Requests[^1];
        Assert.Equal(HttpMethod.Post, detach.Method);
        Assert.Equal($"/v1/payment_methods/{PaymentMethodId}/detach", detach.Path);
        Assert.Equal($"/v1/subscriptions", transport.Requests[2].Path);
        Assert.Contains($"customer={CustomerId}", transport.Requests[2].Query);
    }

    [Fact]
    public async Task A_Card_A_Live_Subscription_Renews_On_Stays_On_The_Customer()
    {
        var transport = new RecordingHandler(
            PaymentMethod(CustomerId),
            Customer(defaultPaymentMethod: null),
            $$"""{"object":"list","data":[{"id":"sub_plus","object":"subscription","status":"active","default_payment_method":"{{PaymentMethodId}}"}],"has_more":false,"url":"/v1/subscriptions"}""");

        await Client(transport).DetachSavedCardAsync(CustomerId, PaymentMethodId, CancellationToken.None);

        Assert.DoesNotContain(transport.Requests, r => r.Path.EndsWith("/detach"));
    }

    [Fact]
    public async Task A_Card_The_Customers_Invoices_Default_To_Stays_On_The_Customer()
    {
        var transport = new RecordingHandler(PaymentMethod(CustomerId), Customer(defaultPaymentMethod: PaymentMethodId));

        await Client(transport).DetachSavedCardAsync(CustomerId, PaymentMethodId, CancellationToken.None);

        Assert.DoesNotContain(transport.Requests, r => r.Path.EndsWith("/detach"));
    }

    [Fact]
    public async Task A_Card_Already_Off_The_Customer_Is_Left_Alone()
    {
        var transport = new RecordingHandler(PaymentMethod(customer: null));

        await Client(transport).DetachSavedCardAsync(CustomerId, PaymentMethodId, CancellationToken.None);

        Assert.Single(transport.Requests);
    }

    private static string PaymentMethod(string? customer) =>
        $$"""{"id":"{{PaymentMethodId}}","object":"payment_method","type":"card","customer":{{(customer is null ? "null" : $"\"{customer}\"")}}}""";

    private static string Customer(string? defaultPaymentMethod) =>
        $$$"""{"id":"{{{CustomerId}}}","object":"customer","invoice_settings":{"default_payment_method":{{{(defaultPaymentMethod is null ? "null" : $"\"{defaultPaymentMethod}\"")}}}}}""";

    private static StripeClient Client(RecordingHandler transport) => new(
        new StubStripeConfig(),
        new StubHttpClientFactory(new HttpClient(transport)),
        NullLogger<StripeClient>.Instance);

    private sealed class RecordingHandler(params string[] responses) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string Query)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, Uri.UnescapeDataString(request.RequestUri.Query)));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[Requests.Count - 1], Encoding.UTF8, "application/json"),
            });
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
