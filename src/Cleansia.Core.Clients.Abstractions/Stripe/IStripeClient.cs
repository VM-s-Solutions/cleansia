using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Clients.Abstractions.Stripe;

public interface IStripeClient
{
    Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken);
}