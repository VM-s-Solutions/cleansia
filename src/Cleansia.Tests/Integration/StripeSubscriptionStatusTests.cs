using System.Net;
using System.Text;
using System.Text.Json;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

public class StripeSubscriptionStatusTests
{
    [Theory]
    [InlineData(false, "active")]
    [InlineData(false, "incomplete")]
    [InlineData(false, "trialing")]
    [InlineData(true, "active")]
    [InlineData(true, "past_due")]
    [InlineData(true, "trialing")]
    public async Task Create_and_swap_carry_the_actual_subscription_status(bool swap, string status)
    {
        var transport = new SubscriptionResponse(status);
        var client = new StripeClient(new StubStripeConfig(), new StubHttpClientFactory(new HttpClient(transport)),
            NullLogger<StripeClient>.Instance);

        var result = swap
            ? await client.SwapSubscriptionPriceAsync("sub_unit", "price_yearly", "attempt", CancellationToken.None)
            : await client.CreateSubscriptionAsync("cus_unit", "price_monthly", 0, "attempt", CancellationToken.None);

        Assert.Equal(status, result.Status);
        Assert.Equal("sub_unit", result.SubscriptionId);
        Assert.Equal(swap ? 2 : 1, transport.Requests);
    }

    private sealed class SubscriptionResponse(string status) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var body = JsonSerializer.Serialize(new
            {
                id = "sub_unit", @object = "subscription", status,
                items = new
                {
                    @object = "list",
                    data = new[] { new { id = "si_unit", @object = "subscription_item", current_period_start = 1789516800L, current_period_end = 1792108800L } },
                },
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
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
