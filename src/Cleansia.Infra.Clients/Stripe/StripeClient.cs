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

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Order order, CancellationToken cancellationToken)
    {
        if (order.StripeSessionId is { Length: > 0 } currentSessionId)
        {
            var current = await ClassifyAsync(
                nameof(CreateCheckoutSessionAsync),
                () => new SessionService(stripe).GetAsync(currentSessionId, cancellationToken: cancellationToken));
            if (current.Status == "open")
            {
                return new CheckoutSessionResult(current.Id, current.Url);
            }
        }

        return await CreateCheckoutSessionAsync(
            order,
            expiresAtUtc: null,
            $"checkout-{order.Id}",
            $"{config.CancelUrlBase}?orderId={order.Id}",
            cardToSave: null,
            cancellationToken);
    }

    public Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(
        Order order, string stripeCustomerId, string savedCardId, CancellationToken cancellationToken) =>
        CreateCheckoutSessionAsync(
            order,
            expiresAtUtc: null,
            $"checkout-{order.Id}",
            $"{config.CancelUrlBase}?orderId={order.Id}",
            (stripeCustomerId, savedCardId),
            cancellationToken);

    public Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Order order, DateTime expiresAtUtc, CancellationToken cancellationToken) =>
        CreateExpiringCheckoutSessionAsync(order, expiresAtUtc, cardToSave: null, cancellationToken);

    public Task<CheckoutSessionResult> CreateCardSavingCheckoutSessionAsync(
        Order order, DateTime expiresAtUtc, string stripeCustomerId, string savedCardId, CancellationToken cancellationToken) =>
        CreateExpiringCheckoutSessionAsync(order, expiresAtUtc, (stripeCustomerId, savedCardId), cancellationToken);

    private async Task<CheckoutSessionResult> CreateExpiringCheckoutSessionAsync(
        Order order,
        DateTime expiresAtUtc,
        (string StripeCustomerId, string SavedCardId)? cardToSave,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        var idempotencyKey = $"checkout-{order.Id}-{new DateTimeOffset(expiresAt).ToUnixTimeSeconds()}";
        if (order.StripeSessionId is { Length: > 0 } currentSessionId)
        {
            var service = new SessionService(stripe);
            var current = await ClassifyAsync(
                nameof(CreateCheckoutSessionAsync),
                () => service.GetAsync(currentSessionId, cancellationToken: cancellationToken));
            if (current.Status == "open")
            {
                var currentSavesCard = current.Metadata?.ContainsKey(SavedCardMetadataKey) ?? false;
                if (current.AmountTotal == ToMinorUnits(order.AmountDueOnCard)
                    && currentSavesCard == cardToSave.HasValue)
                {
                    return new CheckoutSessionResult(current.Id, current.Url);
                }

                // Closed before its replacement opens, so the customer can never pay both. A session the
                // customer completes in between cannot be expired, and the throw refuses the replacement.
                await ClassifyAsync(
                    nameof(CreateCheckoutSessionAsync),
                    () => service.ExpireAsync(currentSessionId, cancellationToken: cancellationToken));
            }

            // An expired session may be one this client expired a moment ago, whose replacement is now asked
            // for again at the next stride - under that stride's plain key, which the expired session may
            // itself have been opened under. A paid session keeps the plain key, so its replay opens no second
            // payable session before its webhook lands.
            if (current.Status != "complete")
            {
                idempotencyKey += $"-after-{currentSessionId}";
            }
        }

        return await CreateCheckoutSessionAsync(
            order,
            expiresAt,
            idempotencyKey,
            new Uri(config.SuccessUrlBase).GetLeftPart(UriPartial.Authority) + $"{OrdersPagePath}/{order.Id}",
            cardToSave,
            cancellationToken);
    }

    private async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(
        Order order,
        DateTime? expiresAtUtc,
        string idempotencyKey,
        string cancelUrl,
        (string StripeCustomerId, string SavedCardId)? cardToSave,
        CancellationToken cancellationToken)
    {
        // AmountDueOnCard, not TotalPrice: credit is a tender, so the sale keeps its size and only
        // the figure the card is asked for moves. Charging TotalPrice here would take the credit AND
        // the full amount. -> Order.CreditAppliedAmount
        var unitAmount = ToMinorUnits(order.AmountDueOnCard);

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = (order.Currency?.Code
                                    ?? throw new InvalidOperationException(
                                        $"Order {order.Id} has no resolved currency; a checkout session cannot be denominated."))
                            .ToLower(),
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
            // Adaptive Pricing OFF, explicitly. Left to the account default, Stripe may present this
            // session in the buyer's local currency and charge them a 2-4% conversion fee, while the
            // order row, the receipt and every report still say CZK. It applies to exactly this shape
            // — a Checkout Session with an inline `PriceData` — and NOT to the mobile PaymentIntent
            // path below, so the two channels would silently disagree.
            //
            // Setting `Currency` here does NOT disable it: Stripe's restrictions list is a closed set
            // that does not include the session currency, which is the FROM currency rather than an
            // off-switch. This property is the only lever in the API.
            //
            // The dashboard toggle (off as of 2026-09-08) is the primary defence, because Payment
            // Links are reachable by nobody in this file. This is defence in depth against it being
            // flipped back.
            AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false },
            SuccessUrl = $"{config.SuccessUrlBase}?session_id={{CHECKOUT_SESSION_ID}}&orderId={order.Id}",
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string> { { "OrderId", order.Id } },
            ExpiresAt = expiresAtUtc,
        };

        if (cardToSave is { } saving)
        {
            options.Customer = saving.StripeCustomerId;
            options.Metadata[SavedCardMetadataKey] = saving.SavedCardId;
            options.PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                SetupFutureUsage = "off_session",
                Metadata = new Dictionary<string, string> { { SavedCardMetadataKey, saving.SavedCardId } },
            };
        }

        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
        var service = new SessionService(stripe);
        var session = await ClassifyAsync(
            nameof(CreateCheckoutSessionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));

        return new CheckoutSessionResult(session.Id, session.Url);
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
            Metadata = new Dictionary<string, string> { { RefundKeyMetadataKey, idempotencyKey } },
        };
        // The caller's deterministic refund key is the idempotency key (ADR-0006 D3), so Stripe replays its
        // original refund to a retry for about a day; the RefundKey metadata is what a later retry finds it by.
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
            Metadata = new Dictionary<string, string> { { RefundKeyMetadataKey, idempotencyKey } },
        };
        // The caller's deterministic refund key is the idempotency key (ADR-0006 D3), so Stripe replays its
        // original refund to a retry for about a day; the RefundKey metadata is what a later retry finds it by.
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
        await ClassifyAsync(
            nameof(RefundPaymentIntentAsync),
            () => refundService.CreateAsync(refundOptions, requestOptions, cancellationToken));
    }

    public async Task<StripeRefundSnapshot?> FindRefundAsync(
        string? stripeSessionId,
        string? stripePaymentIntentId,
        string refundKey,
        CancellationToken cancellationToken)
    {
        var intentId = stripePaymentIntentId;
        if (!string.IsNullOrEmpty(stripeSessionId))
        {
            var sessionService = new SessionService(stripe);
            var session = await ClassifyAsync(
                nameof(FindRefundAsync),
                () => sessionService.GetAsync(stripeSessionId, cancellationToken: cancellationToken));
            intentId = session.PaymentIntentId;
        }

        if (string.IsNullOrEmpty(intentId))
        {
            return null;
        }

        var refundService = new global::Stripe.RefundService(stripe);
        var ours = await ClassifyAsync(nameof(FindRefundAsync), async () =>
        {
            var found = new List<global::Stripe.Refund>();
            await foreach (var refund in refundService.ListAutoPagingAsync(
                               new global::Stripe.RefundListOptions { PaymentIntent = intentId, Limit = 100 },
                               cancellationToken: cancellationToken))
            {
                if (refund.Metadata?.GetValueOrDefault(RefundKeyMetadataKey) == refundKey)
                {
                    found.Add(refund);
                }
            }

            return found;
        });

        var made = ours.FirstOrDefault(r => !IsFailedRefund(r.Status)) ?? ours.FirstOrDefault();
        return made is null
            ? null
            : new StripeRefundSnapshot(made.Id, made.Amount / 100m, IsFailedRefund(made.Status));
    }

    private static bool IsFailedRefund(string status) => status is "failed" or "canceled";

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
        string? savedCardId,
        string? currentPaymentIntentId,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(stripe);
        var amountCents = ToMinorUnits(amount);
        if (!string.IsNullOrEmpty(currentPaymentIntentId))
        {
            var current = await ClassifyAsync(
                nameof(CreatePaymentIntentAsync),
                () => service.GetAsync(currentPaymentIntentId, cancellationToken: cancellationToken));
            var currentSavesCard = current.Metadata?.ContainsKey(SavedCardMetadataKey) ?? false;
            if (current.Status is "requires_payment_method" or "requires_confirmation" or "requires_action"
                && current.Amount == amountCents
                && current.CustomerId == stripeCustomerId
                && currentSavesCard == !string.IsNullOrEmpty(savedCardId))
            {
                return new PaymentIntentResult(current.Id, current.ClientSecret);
            }
        }

        var options = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency.ToLowerInvariant(),
            Customer = stripeCustomerId,
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
        if (!string.IsNullOrEmpty(savedCardId))
        {
            options.SetupFutureUsage = "off_session";
            options.Metadata[SavedCardMetadataKey] = savedCardId;
        }

        // A retry of this very request replays this intent. A replacement is keyed on the intent it
        // replaces, so returning to an earlier amount or tick never replays an intent already cancelled,
        // and a card-saving intent on its own saved-card row, since its parameters differ from the plain one's.
        var idempotencyKey = $"pi-{orderId}-{amountCents}"
                             + (string.IsNullOrEmpty(currentPaymentIntentId) ? string.Empty : $"-after-{currentPaymentIntentId}")
                             + (string.IsNullOrEmpty(savedCardId) ? string.Empty : $"-card-{savedCardId}");
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
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

    public async Task CancelReplacedPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(stripe);
        var options = new PaymentIntentCancelOptions { CancellationReason = ReplacedIntentCancellationReason };
        await ClassifyAsync(
            nameof(CancelReplacedPaymentIntentAsync),
            () => service.CancelAsync(paymentIntentId, options, cancellationToken: cancellationToken));
    }

    public async Task<StripePaymentSnapshot> GetPaymentSnapshotAsync(
        string? stripeSessionId,
        string? stripePaymentIntentId,
        CancellationToken cancellationToken)
    {
        var intentId = stripePaymentIntentId;

        if (string.IsNullOrEmpty(intentId) && !string.IsNullOrEmpty(stripeSessionId))
        {
            var sessionService = new SessionService(stripe);
            var session = await ClassifyAsync(
                nameof(GetPaymentSnapshotAsync),
                () => sessionService.GetAsync(stripeSessionId, cancellationToken: cancellationToken));

            if (session.PaymentStatus is "paid" or "no_payment_required")
            {
                return new StripePaymentSnapshot(StripePaymentState.Settled, null);
            }

            intentId = session.PaymentIntentId;
        }

        if (string.IsNullOrEmpty(intentId))
        {
            return new StripePaymentSnapshot(StripePaymentState.Unpaid, null);
        }

        var intentService = new PaymentIntentService(stripe);
        var intent = await ClassifyAsync(
            nameof(GetPaymentSnapshotAsync),
            () => intentService.GetAsync(intentId, cancellationToken: cancellationToken));

        var state = MapIntentStatus(intent.Status);
        var cancellable = state == StripePaymentState.Unpaid && intent.Status != "canceled" ? intent.Id : null;
        return new StripePaymentSnapshot(state, cancellable);
    }

    public async Task<string?> FindCheckoutSessionOrderIdAsync(
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var sessionService = new SessionService(stripe);
        var sessions = await ClassifyAsync(
            nameof(FindCheckoutSessionOrderIdAsync),
            () => sessionService.ListAsync(
                new SessionListOptions { PaymentIntent = paymentIntentId, Limit = 1 },
                cancellationToken: cancellationToken));

        return sessions.Data.FirstOrDefault()?.Metadata?.GetValueOrDefault("OrderId");
    }

    // requires_capture is an authorized hold — real money the customer can still be charged — so it
    // counts as in-flight, not unpaid. requires_action is a customer mid-3DS who may confirm a second
    // later. Everything the customer has not started (or has abandoned/cancelled) is genuinely unpaid.
    private static StripePaymentState MapIntentStatus(string status) => status switch
    {
        "succeeded" => StripePaymentState.Settled,
        "processing" or "requires_capture" or "requires_action" => StripePaymentState.Processing,
        _ => StripePaymentState.Unpaid,
    };

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

    public async Task<SetupIntentResult> CreateCardSetupIntentAsync(
        string stripeCustomerId,
        string savedCardId,
        CancellationToken cancellationToken)
    {
        var service = new SetupIntentService(stripe);
        var options = new SetupIntentCreateOptions
        {
            Customer = stripeCustomerId,
            Usage = "off_session",
            PaymentMethodTypes = ["card"],
            Metadata = new Dictionary<string, string> { { SavedCardMetadataKey, savedCardId } },
        };
        var requestOptions = new RequestOptions { IdempotencyKey = $"saved-card-setup-{savedCardId}" };
        var intent = await ClassifyAsync(
            nameof(CreateCardSetupIntentAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
        return new SetupIntentResult(intent.Id, intent.ClientSecret);
    }

    public async Task<string> CreateCardSetupCheckoutSessionAsync(
        string stripeCustomerId,
        string savedCardId,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string> { { SavedCardMetadataKey, savedCardId } };
        var profileUrl = new Uri(config.SuccessUrlBase).GetLeftPart(UriPartial.Authority) + ProfilePagePath;
        var options = new SessionCreateOptions
        {
            Mode = "setup",
            Customer = stripeCustomerId,
            PaymentMethodTypes = ["card"],
            SetupIntentData = new SessionSetupIntentDataOptions { Metadata = metadata },
            Metadata = metadata,
            SuccessUrl = $"{profileUrl}?cardSetup=success",
            CancelUrl = $"{profileUrl}?cardSetup=cancel",
        };
        var requestOptions = new RequestOptions { IdempotencyKey = $"saved-card-checkout-{savedCardId}" };
        var service = new SessionService(stripe);
        var session = await ClassifyAsync(
            nameof(CreateCardSetupCheckoutSessionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
        return session.Url;
    }

    public async Task<SavedCardDetails?> GetSetupIntentCardAsync(
        string setupIntentId,
        CancellationToken cancellationToken)
    {
        var service = new SetupIntentService(stripe);
        var options = new SetupIntentGetOptions { Expand = ["payment_method"] };
        var intent = await ClassifyAsync(
            nameof(GetSetupIntentCardAsync),
            () => service.GetAsync(setupIntentId, options, cancellationToken: cancellationToken));

        if (intent.Status != "succeeded" || intent.PaymentMethod is not { Card: { } card } paymentMethod)
        {
            return null;
        }

        return new SavedCardDetails(paymentMethod.Id, card.Brand, card.Last4, (int)card.ExpMonth, (int)card.ExpYear);
    }

    public async Task<SavedCardDetails?> GetPaymentIntentCardAsync(
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(stripe);
        var options = new PaymentIntentGetOptions { Expand = ["payment_method"] };
        var intent = await ClassifyAsync(
            nameof(GetPaymentIntentCardAsync),
            () => service.GetAsync(paymentIntentId, options, cancellationToken: cancellationToken));

        if (intent.Status != "succeeded"
            || intent.SetupFutureUsage != "off_session"
            || intent.PaymentMethod is not { Card: { } card } paymentMethod)
        {
            return null;
        }

        return new SavedCardDetails(paymentMethod.Id, card.Brand, card.Last4, (int)card.ExpMonth, (int)card.ExpYear);
    }

    public async Task<string> ChargeReceivableOffSessionAsync(
        string receivableId,
        decimal amount,
        string currency,
        string stripeCustomerId,
        string paymentMethodId,
        int attempt,
        CancellationToken cancellationToken)
    {
        var service = new PaymentIntentService(stripe);
        var options = new PaymentIntentCreateOptions
        {
            Amount = ToMinorUnits(amount),
            Currency = currency.ToLowerInvariant(),
            Customer = stripeCustomerId,
            PaymentMethod = paymentMethodId,
            PaymentMethodTypes = ["card"],
            OffSession = true,
            Confirm = true,
            Metadata = new Dictionary<string, string> { { ReceivableMetadataKey, receivableId } },
        };
        var requestOptions = new RequestOptions { IdempotencyKey = $"receivable-charge-{receivableId}-{attempt}" };
        var intent = await ClassifyAsync(
            nameof(ChargeReceivableOffSessionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
        return intent.Id;
    }

    public async Task<CheckoutSessionResult> CreateReceivableCheckoutSessionAsync(
        string receivableId,
        string? currentSessionId,
        string orderId,
        string displayOrderNumber,
        decimal amount,
        string currency,
        CancellationToken cancellationToken)
    {
        var service = new SessionService(stripe);
        if (!string.IsNullOrEmpty(currentSessionId))
        {
            var current = await ClassifyAsync(
                nameof(CreateReceivableCheckoutSessionAsync),
                () => service.GetAsync(currentSessionId, cancellationToken: cancellationToken));
            if (current.Status == "open" && current.ExpiresAt > DateTime.UtcNow)
            {
                return new CheckoutSessionResult(current.Id, current.Url);
            }
        }

        var orderPage = new Uri(config.SuccessUrlBase).GetLeftPart(UriPartial.Authority) + $"{OrdersPagePath}/{orderId}";
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Amount due on order {displayOrderNumber}",
                        },
                        UnitAmount = ToMinorUnits(amount),
                    },
                    Quantity = 1,
                },
            ],
            AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false },
            SuccessUrl = orderPage,
            CancelUrl = orderPage,
            Metadata = new Dictionary<string, string> { { ReceivableMetadataKey, receivableId } },
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = string.IsNullOrEmpty(currentSessionId)
                ? $"receivable-checkout-{receivableId}"
                : $"receivable-checkout-{receivableId}-after-{currentSessionId}",
        };
        var session = await ClassifyAsync(
            nameof(CreateReceivableCheckoutSessionAsync),
            () => service.CreateAsync(options, requestOptions, cancellationToken));
        return new CheckoutSessionResult(session.Id, session.Url);
    }

    public async Task<bool> ExpireReceivableCheckoutSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var service = new SessionService(stripe);
        var session = await ClassifyAsync(
            nameof(ExpireReceivableCheckoutSessionAsync),
            () => service.GetAsync(sessionId, cancellationToken: cancellationToken));
        if (session.Status == "complete")
        {
            return false;
        }

        if (session.Status == "open")
        {
            await ClassifyAsync(
                nameof(ExpireReceivableCheckoutSessionAsync),
                () => service.ExpireAsync(sessionId, cancellationToken: cancellationToken));
        }

        return true;
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
            CurrentPeriodEnd: periodEnd,
            TrialEnd: subscription.TrialEnd,
            Status: subscription.Status);
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
            CurrentPeriodEnd: periodEnd,
            TrialEnd: swapped.TrialEnd,
            Status: swapped.Status);
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

    public async Task CancelSubscriptionNowAsync(
        string stripeSubscriptionId,
        CancellationToken cancellationToken)
    {
        var service = new SubscriptionService(stripe);
        var options = new SubscriptionCancelOptions
        {
            InvoiceNow = false,
            Prorate = false,
            Expand = ["latest_invoice"],
        };
        var requestOptions = new RequestOptions { IdempotencyKey = $"cancel-now-{stripeSubscriptionId}" };
        var cancelled = await ClassifyAsync(
            nameof(CancelSubscriptionNowAsync),
            () => service.CancelAsync(stripeSubscriptionId, options, requestOptions, cancellationToken));

        // Cancelling stops Stripe collecting the open invoice but leaves it open and owed; voiding it is
        // what makes "nothing more is charged" true.
        if (cancelled.LatestInvoice is { Status: "open" } invoice)
        {
            await ClassifyAsync(
                nameof(CancelSubscriptionNowAsync),
                () => new InvoiceService(stripe).VoidInvoiceAsync(
                    invoice.Id,
                    options: null,
                    new RequestOptions { IdempotencyKey = $"void-{invoice.Id}" },
                    cancellationToken));
        }
    }

    public async Task<string> CreateMembershipCheckoutSessionAsync(
        string stripeCustomerId,
        string stripePriceId,
        string userId,
        string membershipPlanCode,
        int trialPeriodDays,
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
            // Adaptive Pricing OFF — see the note on the order session above. This path is the WORSE
            // of the two to leave open: a subscription converted at checkout locks that currency for
            // its lifetime (Stripe refuses to change a live subscription's currency) and re-fetches a
            // real-time rate every billing cycle, so the price drifts. Turning the feature off later
            // does not unwind one that already exists.
            AdaptivePricing = new SessionAdaptivePricingOptions { Enabled = false },
            Customer = stripeCustomerId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = stripePriceId,
                    Quantity = 1,
                },
            ],
            SuccessUrl = MembershipReturnUrl(MembershipWelcomePath),
            CancelUrl = MembershipReturnUrl(PlusPagePath),
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

    // The customer-app routes a checkout returns to: the two of a membership checkout, the orders
    // mount an expiring order checkout cancels back to, and the profile a card setup returns to. Pinned by
    // MembershipReturnPathTests, which reads them back out of the Angular route table — because a
    // frontend path living in a backend assembly is invisible to `nx affected`, to every Angular
    // test, and to the compiler, which is precisely how SuccessUrlBase came to point at the partner
    // app's port for as long as it did.
    private const string MembershipWelcomePath = "/membership/welcome";
    private const string PlusPagePath = "/plus";
    private const string OrdersPagePath = "/orders";
    private const string ProfilePagePath = "/profile";

    private const string SavedCardMetadataKey = "SavedCardId";

    private const string ReplacedIntentCancellationReason = "duplicate";

    private const string ReceivableMetadataKey = "ReceivableId";

    private const string RefundKeyMetadataKey = "RefundKey";

    /// <summary>
    /// Where Stripe sends the browser back to after a membership checkout.
    /// <para>
    /// The ORIGIN comes from <c>Stripe:SuccessUrlBase</c> — the same value the order flow returns
    /// to — and the PATH is a constant here. Neither comes from the caller any more. Deriving it
    /// rather than accepting it is the whole fix: an authenticated caller used to hand these
    /// straight to Stripe, so it could send a paying customer anywhere, including a page that looked
    /// like ours.
    /// </para>
    /// <para>
    /// A consequence worth knowing rather than fixing here: auth cookies are host-scoped
    /// (AuthCookieWriter sets no Domain), so a visitor browsing a non-canonical host — www, say —
    /// returns to the canonical one and arrives signed out. That is already true of every card
    /// ORDER, which has derived its return URL from this same key since it was written; this makes
    /// membership consistent with it rather than introducing anything new.
    /// </para>
    /// </summary>
    private string MembershipReturnUrl(string path) =>
        new Uri(config.SuccessUrlBase).GetLeftPart(UriPartial.Authority) + path;

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
