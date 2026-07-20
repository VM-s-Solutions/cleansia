using Cleansia.Core.Clients.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using IStripeClient = Cleansia.Core.Clients.Abstractions.Stripe.IStripeClient;

namespace Cleansia.Infra.Clients.Stripe;

public class StripeClient : IStripeClient
{
    private readonly IStripeConfig config;
    private readonly ILogger logger;
    private readonly global::Stripe.StripeClient stripe;

    public StripeClient(
        IStripeConfig config,
        IHttpClientFactory httpClientFactory,
        ILogger<StripeClient> logger)
    {
        this.config = config;
        this.logger = logger;

        // Hand the SDK the pooled, factory-managed HttpClient (resilience handler + OTel), built once
        // and reused by every *Service below, instead of minting its own socket per call.
        //
        // maxNetworkRetries: 0 — retry lives at the named client's resilience handler, not in the SDK,
        // so we don't double-retry. The idempotency keys on each write's RequestOptions keep any
        // transport-level retry safe.
        var transport = httpClientFactory.CreateClient(StripeExtensions.HttpClientName);
        var systemNetHttpClient = new SystemNetHttpClient(transport, maxNetworkRetries: 0);
        stripe = new global::Stripe.StripeClient(config.SecretKey, httpClient: systemNetHttpClient);
    }

    // Stripe amounts are integer minor units. A bare (long) cast truncates toward zero, so any
    // amount still carrying fractional cents would silently lose one against the ledger's
    // numeric(18,2) away-from-zero rounding — round the same way here so Stripe and the persisted
    // amount are always cent-identical.
    public static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public async Task<string> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken)
    {
        var unitAmount = ToMinorUnits(order.TotalPrice);

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
        var service = new SessionService(stripe);
        var session = await ClassifyAsync(
            nameof(CreateCheckoutSessionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));

        return session.Url;
    }

    public async Task RefundCheckoutSessionAsync(
        string stripeSessionId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var sessionService = new SessionService(stripe);
        var session = await ClassifyAsync(
            nameof(RefundCheckoutSessionAsync),
            () => sessionService.GetAsync(stripeSessionId, cancellationToken: cancellationToken));

        if (string.IsNullOrEmpty(session.PaymentIntentId))
        {
            throw new InvalidOperationException(
                $"Checkout session {stripeSessionId} has no PaymentIntent — likely unpaid, nothing to refund.");
        }

        var refundService = new global::Stripe.RefundService(stripe);
        var refundOptions = new global::Stripe.RefundCreateOptions
        {
            PaymentIntent = session.PaymentIntentId,
            Amount = ToMinorUnits(amount),
            Reason = global::Stripe.RefundReasons.RequestedByCustomer,
        };
        // The caller's deterministic refund key is the idempotency key (ADR-0006 D3): a retry of the
        // same logical refund replays Stripe's original refund instead of issuing a second one.
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
        await ClassifyAsync(
            nameof(RefundCheckoutSessionAsync),
            () => refundService.CreateAsync(refundOptions, requestOptions, cancellationToken));
    }

    public async Task RefundPaymentIntentAsync(
        string paymentIntentId, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        var refundService = new global::Stripe.RefundService(stripe);
        var refundOptions = new global::Stripe.RefundCreateOptions
        {
            PaymentIntent = paymentIntentId,
            Amount = ToMinorUnits(amount),
            Reason = global::Stripe.RefundReasons.RequestedByCustomer,
        };
        // The caller's deterministic refund key is the idempotency key (ADR-0006 D3): a retry of the
        // same logical refund replays Stripe's original refund instead of issuing a second one.
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
        await ClassifyAsync(
            nameof(RefundPaymentIntentAsync),
            () => refundService.CreateAsync(refundOptions, requestOptions, cancellationToken));
    }

    public async Task<string> CreateCustomerAsync(
        string userId,
        string email,
        string fullName,
        string? phone,
        CancellationToken cancellationToken)
    {
        var service = new CustomerService(stripe);
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
        var customer = await ClassifyAsync(
            nameof(CreateCustomerAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
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
        var service = new PaymentIntentService(stripe);
        var options = new PaymentIntentCreateOptions
        {
            Amount = ToMinorUnits(amount),
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
        var amountCents = ToMinorUnits(amount);
        var requestOptions = new RequestOptions { IdempotencyKey = $"pi-{orderId}-{amountCents}" };
        var intent = await ClassifyAsync(
            nameof(CreatePaymentIntentAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
        return new PaymentIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(stripe);
        // No options → uses the default `requested_by_customer` cancellation
        // reason. Stripe accepts cancel on requires_payment_method,
        // requires_confirmation, requires_action, processing, requires_capture.
        await ClassifyAsync(
            nameof(CancelPaymentIntentAsync),
            () => service.CancelAsync(paymentIntentId, cancellationToken: cancellationToken));
    }

    public async Task<string> CreateEphemeralKeyAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken)
    {
        var service = new EphemeralKeyService(stripe);
        var options = new EphemeralKeyCreateOptions
        {
            Customer = stripeCustomerId,
            StripeVersion = "2024-12-18.acacia",
        };
        var key = await ClassifyAsync(
            nameof(CreateEphemeralKeyAsync),
            () => service.CreateAsync(options, cancellationToken: cancellationToken));
        return key.Secret;
    }

    public async Task<SetupIntentResult> CreateSetupIntentAsync(
        string stripeCustomerId,
        CancellationToken cancellationToken)
    {
        var service = new SetupIntentService(stripe);
        var options = new SetupIntentCreateOptions
        {
            Customer = stripeCustomerId,
            Usage = "off_session",
            AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
            },
        };
        var intent = await ClassifyAsync(
            nameof(CreateSetupIntentAsync),
            () => service.CreateAsync(options, cancellationToken: cancellationToken));
        return new SetupIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<SubscriptionResult> CreateSubscriptionAsync(
        string stripeCustomerId,
        string stripePriceId,
        int trialPeriodDays,
        string idempotencyAttemptId,
        CancellationToken cancellationToken)
    {
        var service = new SubscriptionService(stripe);
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
        var subscription = await ClassifyAsync(
            nameof(CreateSubscriptionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
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
        var service = new SubscriptionService(stripe);

        var existing = await ClassifyAsync(
            nameof(SwapSubscriptionPriceAsync),
            () => service.GetAsync(stripeSubscriptionId, cancellationToken: cancellationToken));
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
        var swapped = await ClassifyAsync(
            nameof(SwapSubscriptionPriceAsync),
            () => service.UpdateAsync(stripeSubscriptionId, options, requestOptions, cancellationToken));
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
        var service = new SubscriptionService(stripe);
        var options = new SubscriptionUpdateOptions { CancelAtPeriodEnd = true };
        var requestOptions = new RequestOptions { IdempotencyKey = $"cancel-{stripeSubscriptionId}" };
        await ClassifyAsync(
            nameof(CancelSubscriptionAtPeriodEndAsync),
            () => service.UpdateAsync(stripeSubscriptionId, options, requestOptions, cancellationToken));
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
        var service = new SessionService(stripe);
        var session = await ClassifyAsync(
            nameof(CreateMembershipCheckoutSessionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
        return session.Url;
    }

    // Classify + meter + log every Stripe failure at the adapter boundary, then re-throw so the
    // existing caller contracts (callers handle StripeException; the handler shapes the BusinessResult)
    // are unchanged. This adds observability only; it does not alter the throw/return.
    private async Task<T> ClassifyAsync<T>(string operation, Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (StripeException ex)
        {
            LogBoundary(operation, IntegrationFailureClassifier.FromStripeException(ex), ex);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            LogBoundary(operation, IntegrationFailureClassifier.FromException(ex), ex);
            throw;
        }
    }

    private void LogBoundary(string operation, IntegrationFailureClass failureClass, Exception ex)
    {
        IntegrationFailureMetrics.Record(StripeExtensions.HttpClientName, failureClass);

        if (failureClass == IntegrationFailureClass.AuthConfig)
        {
            // Our key/config is wrong — an ops incident, not a caller error.
            logger.LogError(ex,
                "Stripe {Operation} failed: {FailureClass} (provider config/credentials).",
                operation, failureClass);
        }
        else
        {
            logger.LogWarning(ex,
                "Stripe {Operation} failed: {FailureClass}.",
                operation, failureClass);
        }
    }
}
