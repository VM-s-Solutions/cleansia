using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Stripe;
using Stripe.Checkout;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Infra.Clients.Stripe;

public class StripeClient(IStripeConfig config) : IStripeClient
{
    public async Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken)
    {
        var unitAmount = (long)(order.TotalPrice * 100);

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

        var requestOptions = new RequestOptions { IdempotencyKey = $"checkout-{order.Id}" };
        var service = new SessionService(new global::Stripe.StripeClient(config.SecretKey));
        var session = await service.CreateAsync(options, requestOptions, cancellationToken);

        return session.Url;
    }

    public async Task RefundCheckoutSessionAsync(string stripeSessionId, decimal amount, CancellationToken cancellationToken)
    {
        var client = new global::Stripe.StripeClient(config.SecretKey);

        var sessionService = new SessionService(client);
        var session = await sessionService.GetAsync(stripeSessionId, cancellationToken: cancellationToken);

        if (string.IsNullOrEmpty(session.PaymentIntentId))
        {
            throw new InvalidOperationException(
                $"Checkout session {stripeSessionId} has no PaymentIntent — likely unpaid, nothing to refund.");
        }

        var refundService = new global::Stripe.RefundService(client);
        var refundOptions = new global::Stripe.RefundCreateOptions
        {
            PaymentIntent = session.PaymentIntentId,
            Amount = (long)(amount * 100),
            Reason = global::Stripe.RefundReasons.RequestedByCustomer,
        };
        // Include the amount in the key so partial + later top-up refunds against
        // the same session get distinct keys. Identical re-requests still
        // collide and Stripe returns the original refund (the desired idempotency).
        var amountCents = (long)(amount * 100);
        var requestOptions = new RequestOptions { IdempotencyKey = $"refund-{stripeSessionId}-{amountCents}" };
        await refundService.CreateAsync(refundOptions, requestOptions, cancellationToken);
    }

    public async Task<string> CreateCustomerAsync(
        string userId,
        string email,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var service = new CustomerService(new global::Stripe.StripeClient(config.SecretKey));
        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = fullName,
            Phone = phone,
            Metadata = new Dictionary<string, string>
            {
                { "source", "cleansia" },
                { "userId", userId },
            },
        };
        var requestOptions = new RequestOptions { IdempotencyKey = $"customer-{userId}" };
        var customer = await service.CreateAsync(options, requestOptions, cancellationToken);
        return customer.Id;
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
        decimal amount,
        string currency,
        string stripeCustomerId,
        string orderId,
        string displayOrderNumber,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(new global::Stripe.StripeClient(config.SecretKey));
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100),
            Currency = currency.ToLowerInvariant(),
            Customer = stripeCustomerId,
            SetupFutureUsage = "off_session",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
            Metadata = new Dictionary<string, string>
            {
                { "OrderId", orderId },
                { "DisplayOrderNumber", displayOrderNumber },
            },
        };
        // Include amount in the key so a customer who edits the order
        // (extras/services change → amount differs) can re-open PaymentSheet
        // without Stripe rejecting on idempotency-replay-with-different-params.
        // Same-amount retries still collide and Stripe returns the original
        // intent (the desired idempotent behavior).
        var amountCents = (long)(amount * 100);
        var requestOptions = new RequestOptions { IdempotencyKey = $"pi-{orderId}-{amountCents}" };
        var intent = await service.CreateAsync(options, requestOptions, cancellationToken);
        return new PaymentIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(new global::Stripe.StripeClient(config.SecretKey));
        // No options → uses the default `requested_by_customer` cancellation
        // reason. Stripe accepts cancel on requires_payment_method,
        // requires_confirmation, requires_action, processing, requires_capture.
        await service.CancelAsync(paymentIntentId, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateEphemeralKeyAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken)
    {
        var service = new EphemeralKeyService(new global::Stripe.StripeClient(config.SecretKey));
        var options = new EphemeralKeyCreateOptions
        {
            Customer = stripeCustomerId,
            StripeVersion = "2024-12-18.acacia",
        };
        var key = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return key.Secret;
    }

    public async Task<SetupIntentResult> CreateSetupIntentAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken)
    {
        var service = new SetupIntentService(new global::Stripe.StripeClient(config.SecretKey));
        var options = new SetupIntentCreateOptions
        {
            Customer = stripeCustomerId,
            Usage = "off_session",
            AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
        };
        var intent = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return new SetupIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<SubscriptionResult> CreateSubscriptionAsync(
        string stripeCustomerId,
        string stripePriceId,
        int trialPeriodDays,
        string idempotencyAttemptId,
        CancellationToken cancellationToken)
    {
        var service = new SubscriptionService(new global::Stripe.StripeClient(config.SecretKey));
        var options = new SubscriptionCreateOptions
        {
            Customer = stripeCustomerId,
            Items =
            [
                new SubscriptionItemOptions { Price = stripePriceId },
            ],
            PaymentBehavior = "default_incomplete",
            PaymentSettings = new SubscriptionPaymentSettingsOptions
            {
                SaveDefaultPaymentMethod = "on_subscription",
            },
            Expand = ["latest_invoice.payment_intent"],
        };
        if (trialPeriodDays > 0)
        {
            options.TrialPeriodDays = trialPeriodDays;
        }
        // attemptId scopes the idempotency to a single user-initiated attempt,
        // so re-subscribing to the same plan after cancellation creates a new
        // subscription instead of returning the canceled one.
        var requestOptions = new RequestOptions { IdempotencyKey = $"sub-{stripeCustomerId}-{stripePriceId}-{idempotencyAttemptId}" };
        var subscription = await service.CreateAsync(options, requestOptions, cancellationToken);
        var firstItem = subscription.Items?.Data?.FirstOrDefault();
        var periodStart = firstItem?.CurrentPeriodStart ?? DateTime.UtcNow;
        var periodEnd = firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);
        return new SubscriptionResult(
            SubscriptionId: subscription.Id,
            CurrentPeriodStart: periodStart,
            CurrentPeriodEnd: periodEnd);
    }

    public async Task<SubscriptionResult> SwapSubscriptionPriceAsync(
        string stripeSubscriptionId,
        string newStripePriceId,
        string idempotencyAttemptId,
        CancellationToken cancellationToken)
    {
        var client = new global::Stripe.StripeClient(config.SecretKey);
        var service = new SubscriptionService(client);

        var existing = await service.GetAsync(stripeSubscriptionId, cancellationToken: cancellationToken);
        var existingItemId = existing.Items?.Data?.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException(
                $"Subscription {stripeSubscriptionId} has no items — can't swap price.");

        var options = new SubscriptionUpdateOptions
        {
            Items =
            [
                new SubscriptionItemOptions
                {
                    Id = existingItemId,
                    Price = newStripePriceId,
                },
            ],
            ProrationBehavior = "always_invoice",
        };
        // attemptId allows a user to swap A→B→A→B and have each swap reach
        // Stripe instead of replaying the first one's response.
        var requestOptions = new RequestOptions { IdempotencyKey = $"swap-{stripeSubscriptionId}-{newStripePriceId}-{idempotencyAttemptId}" };
        var swapped = await service.UpdateAsync(stripeSubscriptionId, options, requestOptions, cancellationToken);
        var firstItem = swapped.Items?.Data?.FirstOrDefault();
        var periodStart = firstItem?.CurrentPeriodStart ?? DateTime.UtcNow;
        var periodEnd = firstItem?.CurrentPeriodEnd ?? DateTime.UtcNow.AddMonths(1);
        return new SubscriptionResult(
            SubscriptionId: swapped.Id,
            CurrentPeriodStart: periodStart,
            CurrentPeriodEnd: periodEnd);
    }

    public async Task CancelSubscriptionAtPeriodEndAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        var service = new SubscriptionService(new global::Stripe.StripeClient(config.SecretKey));
        var options = new SubscriptionUpdateOptions { CancelAtPeriodEnd = true };
        var requestOptions = new RequestOptions { IdempotencyKey = $"cancel-{stripeSubscriptionId}" };
        await service.UpdateAsync(stripeSubscriptionId, options, requestOptions, cancellationToken);
    }

    public async Task<string> CreateMembershipCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        string userId,
        string membershipPlanCode,
        int trialPeriodDays,
        string successUrl,
        string cancelUrl,
        string idempotencyAttemptId,
        CancellationToken cancellationToken)
    {
        var subscriptionData = new SessionSubscriptionDataOptions
        {
            Metadata = new Dictionary<string, string>
            {
                { "UserId", userId },
                { "MembershipPlanCode", membershipPlanCode },
            },
        };
        if (trialPeriodDays > 0)
        {
            subscriptionData.TrialPeriodDays = trialPeriodDays;
        }

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = stripeCustomerId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = stripePriceId,
                    Quantity = 1,
                },
            ],
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            SubscriptionData = subscriptionData,
        };
        // attemptId scopes idempotency to a single open-checkout attempt;
        // re-opening checkout after abandoning produces a fresh Session URL
        // instead of replaying the original (potentially expired) one.
        var requestOptions = new RequestOptions { IdempotencyKey = $"mship-checkout-{userId}-{stripePriceId}-{idempotencyAttemptId}" };
        var service = new SessionService(new global::Stripe.StripeClient(config.SecretKey));
        var session = await service.CreateAsync(options, requestOptions, cancellationToken);
        return session.Url;
    }
}
