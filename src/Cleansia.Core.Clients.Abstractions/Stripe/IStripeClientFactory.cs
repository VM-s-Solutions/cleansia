namespace Cleansia.Core.Clients.Abstractions.Stripe;

public interface IStripeClientFactory
{
    IStripeClient CreateClient();
}