using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging;
using StripeException = Stripe.StripeException;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Default <see cref="IOrderPaymentDispatcher"/>. Wraps the Card/Stripe checkout-session creation and
/// the Cash receipt enqueue with the same narrow <c>StripeException</c> mapping and the same
/// post-commit dispatch seam the handler had inline — extracted verbatim, no behavior change.
/// </summary>
public sealed class OrderPaymentDispatcher(
    IStripeClientFactory stripeClientFactory,
    IPendingDispatch pending,
    ILogger<OrderPaymentDispatcher> logger) : IOrderPaymentDispatcher
{
    public async Task<OrderPaymentDispatchResult> DispatchAsync(
        Order order, string languageCode, CancellationToken cancellationToken)
    {
        switch (order.PaymentType)
        {
            case PaymentType.Card:
                try
                {
                    var stripeClient = stripeClientFactory.CreateClient();
                    var stripeSessionId = await stripeClient.CreateCheckoutSessionAsync(order, cancellationToken);
                    return OrderPaymentDispatchResult.Ok(stripeSessionId);
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
                // ADR-0002 D1/D5 — record intent; PostCommitDispatchBehavior puts it on the wire
                // only after the order row is durably committed (was a before-commit dual-write).
                pending.Enqueue(
                    QueueNames.GenerateReceipt,
                    new QueueEnvelope<GenerateReceiptMessage>(
                        MessageKeys.Receipt(order.Id),
                        order.TenantId,
                        new GenerateReceiptMessage(order.Id, languageCode)),
                    MessageKeys.Receipt(order.Id));
                return OrderPaymentDispatchResult.Ok(null);

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }
}
