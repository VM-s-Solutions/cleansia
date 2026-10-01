using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderPaymentDispatcher"/>. Wraps the Card/Stripe checkout-session creation and
/// the Cash booking e-mail with the same narrow <c>StripeException</c> mapping and the same
/// post-commit dispatch seam the handler had inline.
///
/// One charge surface per card order: the Card branch mints a Checkout Session ONLY on the Web
/// channel. On the Mobile channel it mints nothing here — the in-app Stripe PaymentSheet path
/// (<see cref="CreatePaymentIntent"/>) is the single capturable surface, so the dispatcher returns a
/// null session id to avoid a second, independently-capturable charge surface on the same order.
/// </summary>
public sealed class OrderPaymentDispatcher(
    IStripeClientFactory stripeClientFactory,
    IPendingDispatch pending,
    IOrderChannelProvider channelProvider,
    IStripeConfig stripeConfig,
    IUserRepository userRepository,
    IStripeCustomerResolver stripeCustomerResolver,
    ISavedCardRepository savedCardRepository,
    IRequestMetadataProvider requestMetadataProvider,
    ILogger<OrderPaymentDispatcher> logger) : IOrderPaymentDispatcher
{
    public async Task<OrderPaymentDispatchResult> DispatchAsync(
        Order order, string languageCode, bool saveCard, CancellationToken cancellationToken)
    {
        switch (order.PaymentType)
        {
            case PaymentType.Card:
                // Stripe:Enabled defaults true, so this refuses only when somebody deliberately turned
                // card payments off. Reuses PaymentGatewayUnavailable rather than minting a key: from the
                // customer's side "we cannot take a card right now" is the same fact whether Stripe is
                // down or switched off, and that key already has all five locales in every app.
                if (!stripeConfig.Enabled)
                {
                    logger.LogWarning(
                        "Card payment refused for order {OrderId}: card payments are disabled (Stripe:Enabled=false)",
                        order.Id);
                    return OrderPaymentDispatchResult.Fail(new Error(
                        nameof(PaymentType.Card),
                        BusinessErrorMessage.PaymentGatewayUnavailable));
                }

                if (channelProvider.Channel == OrderChannel.Mobile)
                {
                    return OrderPaymentDispatchResult.Ok(null);
                }

                try
                {
                    var stripeClient = stripeClientFactory.CreateClient();
                    var session = saveCard
                        ? await CreateCardSavingCheckoutSessionAsync(stripeClient, order, cancellationToken)
                        : await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);
                    // The order has to REMEMBER its charge surface. RefundService routes a web order
                    // through RefundCheckoutSessionAsync, which looks the session up by id — and
                    // until this line nothing in production ever called AssignStripeSessionId, so
                    // every web card order fell through to the PaymentIntent branch with a null id.
                    // Assigned before the pipeline commits, on the order this handler already added.
                    order.AssignStripeSessionId(session.Id);
                    // The URL, not the id, is what the browser is redirected to.
                    return OrderPaymentDispatchResult.Ok(session.Url);
                }
                catch (StripeException ex)
                {
                    // Narrow catch: only transient/API-level Stripe failures map to
                    // PaymentGatewayUnavailable. Anything else (DI misconfig, null ref,
                    // bad order state) should bubble as 500 so we see it, not mask it
                    // as a "gateway down" message to the user.
                    logger.LogError(ex, "Stripe checkout session creation failed");
                    return OrderPaymentDispatchResult.Fail(new Error(
                        nameof(PaymentType.Card),
                        BusinessErrorMessage.PaymentGatewayUnavailable));
                }

            case PaymentType.Cash:
                // No money has moved, so there is nothing to receipt yet: the receipt is issued at
                // completion, after the cleaner records the cash (owner ruling 2026-09-28). ADR-0002
                // D1/D5 — recorded as intent and put on the wire only after the order commits.
                OrderBookedEmail.Enqueue(order, languageCode, pending, DateTimeOffset.UtcNow);
                return OrderPaymentDispatchResult.Ok(null);

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    /// <summary>
    /// The saved card is recorded with the consent before the redirect, as the profile's card capture
    /// records it, and the card lands on it when the payment webhook arrives. The validator admits the
    /// tick only from a signed-in customer.
    /// </summary>
    private async Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(
        IStripeClient stripeClient, Order order, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(order.UserId!, cancellationToken)
                   ?? throw new InvalidOperationException($"Order {order.Id} names no customer to save a card for.");
        var currency = order.Currency
                       ?? throw new InvalidOperationException($"Order {order.Id} has no resolved currency to save a card in.");

        var stripeCustomerId = await stripeCustomerResolver.ResolveForCurrencyAsync(user, currency, cancellationToken);
        var card = SavedCard.Start(
            user.Id, currency.Id, stripeCustomerId, requestMetadataProvider.IpAddress, requestMetadataProvider.DeviceLabel);
        var session = await stripeClient.CreateCardSavingCheckoutSessionAsync(
            order, stripeCustomerId, card.Id, cancellationToken);
        savedCardRepository.Add(card);
        return session;
    }
}
