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

        var service = new SessionService(new global::Stripe.StripeClient(config.SecretKey));
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);

        return session.Url;
    }

    public async Task RefundCheckoutSessionAsync(string stripeSessionId, decimal amount, CancellationToken cancellationToken)
    {
        var client = new global::Stripe.StripeClient(config.SecretKey);

        // Look up the checkout session to find the underlying PaymentIntent, which is what we can refund.
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
            Amount = (long)(amount * 100), // Stripe uses minor units
            Reason = global::Stripe.RefundReasons.RequestedByCustomer,
        };
        await refundService.CreateAsync(refundOptions, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateCustomerAsync(
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
            Metadata = new Dictionary<string, string> { { "source", "cleansia" } },
        };
        var customer = await service.CreateAsync(options, cancellationToken: cancellationToken);
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
            // Save the card for future bookings (and future Plus subscription).
            // off_session is what Stripe wants for "we may charge this card later
            // without the user being present" — required for SCA exemption when
            // the user has authenticated this payment.
            SetupFutureUsage = "off_session",
            // Stripe selects the right method (cards, Google Pay, Apple Pay, etc.)
            // based on what's enabled in the account + what the device supports.
            // Required for proper 3DS / SCA challenge handling.
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
        var intent = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return new PaymentIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<string> CreateEphemeralKeyAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken)
    {
        var service = new EphemeralKeyService(new global::Stripe.StripeClient(config.SecretKey));
        // The StripeVersion must match the version the mobile Stripe SDK
        // uses. The mobile team must keep stripe-android in sync; this version
        // pins what the server commits to. If you bump stripe-android, update
        // this string to the version printed in PaymentConfiguration.getInstance().
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
            // off_session usage so the saved payment method can be charged
            // automatically by future subscription invoices without the user
            // present. SCA exemption applies once the user has authenticated
            // the SetupIntent itself.
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
            // Prefer the customer's default payment method (set during the
            // SetupIntent confirm step). If none exists, Stripe returns
            // requires_action and the subscription stays incomplete; we treat
            // that as a failure at the caller.
            PaymentBehavior = "default_incomplete",
            PaymentSettings = new SubscriptionPaymentSettingsOptions
            {
                SaveDefaultPaymentMethod = "on_subscription",
            },
            Expand = ["latest_invoice.payment_intent"],
        };
        // Stripe rejects trial_period_days = 0 as invalid; only set when > 0.
        if (trialPeriodDays > 0)
        {
            options.TrialPeriodDays = trialPeriodDays;
        }
        var subscription = await service.CreateAsync(options, cancellationToken: cancellationToken);
        // Stripe.net 50.x moved per-item period bounds onto SubscriptionItem.
        // For our single-item Plus subscription, the first item's bounds are
        // the subscription's bounds. If the API ever returns zero items
        // (shouldn't happen on create), fall back to UtcNow + month.
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
        CancellationToken cancellationToken)
    {
        var client = new global::Stripe.StripeClient(config.SecretKey);
        var service = new SubscriptionService(client);

        // Need the existing item id so we can replace the price on the same
        // subscription item in-place. Stripe rejects "remove old + add new"
        // when only one item exists — has to be a swap.
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
            // Bill the proration immediately so the swap doesn't sit unbilled
            // until the next renewal. Stripe debits/credits the difference
            // against the customer's default payment method.
            ProrationBehavior = "always_invoice",
        };
        var swapped = await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: cancellationToken);
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
        await service.UpdateAsync(stripeSubscriptionId, options, cancellationToken: cancellationToken);
    }

    public async Task<string> CreateMembershipCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        string userId,
        string membershipPlanCode,
        int trialPeriodDays,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken)
    {
        // Subscription-mode Checkout differs from payment-mode in two ways:
        // (1) Mode = "subscription", (2) line items reference an existing Price
        // id (not inline PriceData) so Stripe can attach recurring billing.
        // Customer subscription metadata is what the webhook reads to resolve
        // the local UserMembership row.
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
        var service = new SessionService(new global::Stripe.StripeClient(config.SecretKey));
        var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
        return session.Url;
    }
}