using System.Net;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Clients.Stripe;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cleansia.Tests.Integration;

/// <summary>
/// An order handed to the checkout seam without its currency navigation is a load that forgot the
/// Include, not an order Stripe may guess a unit for. The seam refuses naming the order, before a
/// single byte reaches Stripe -- a session minted in a guessed currency would charge the card in it.
/// </summary>
public class StripeCheckoutSessionCurrencyTests
{
    [Fact]
    public async Task CreateCheckoutSession_OrderWithoutCurrencyNavigation_ThrowsNamingTheOrder_AndSendsNothing()
    {
        var transport = new RecordingHandler();
        var client = new StripeClient(
            new StubStripeConfig(),
            new StubHttpClientFactory(new HttpClient(transport)),
            NullLogger<StripeClient>.Instance);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Pending,
            userId: "user-1");
        order.Id = "order-no-currency";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.CreateCheckoutSessionAsync(order, CancellationToken.None));

        Assert.Contains(order.Id, ex.Message);
        Assert.Equal(0, transport.Requests);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
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
