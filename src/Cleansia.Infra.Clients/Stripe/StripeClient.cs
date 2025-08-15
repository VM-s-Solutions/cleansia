using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Stripe.Checkout;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Infra.Clients.Stripe;

public class StripeClient(IStripeConfig config) : IStripeClient
{
    public async Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken)
    {
        var unitAmount = (long)(order.TotalPrice * 100);  // Adjust for minor units

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = order.Currency.Code.ToLower(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Cleaning Order #{order.Id}"
                        },
                        UnitAmount = unitAmount
                    },
                    Quantity = 1
                }
            ],
            Mode = "payment",
            SuccessUrl = $"{config.SuccessUrlBase}?session_id={{CHECKOUT_SESSION_ID}}&orderId={order.Id}",
            CancelUrl = $"{config.CancelUrlBase}?orderId={order.Id}",
            Metadata = new Dictionary<string, string> { { "OrderId", order.Id } }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        // TODO: Add to method calling business logic
        //order.StripeSessionId = session.Id;
        //order.PaymentStatus = PaymentStatus.Pending;
        //await _context.SaveChangesAsync();

        return session.Id;
    }
}