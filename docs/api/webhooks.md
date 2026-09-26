# Webhooks

Cleansia receives Stripe webhooks for card payments, Cleansia Plus subscriptions and bank chargebacks. The webhook endpoint is unauthenticated but verified using Stripe's signature mechanism.

::: info Source Files
- Webhook handler: `src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs`
- Partner controller: `src/Cleansia.Web.Partner/Controllers/PaymentController.cs`
- Customer controller: `src/Cleansia.Web.Customer/Controllers/PaymentController.cs`
:::

## Endpoint

```
POST /api/Payment/webhook
```

**Auth:** Anonymous (`[AllowAnonymous]`) -- verified by Stripe signature.

**Headers:**

| Header | Description |
|--------|-------------|
| `Stripe-Signature` | Stripe webhook signature for payload verification |

**Request body:** Raw JSON event payload from Stripe.

**Response:** `200 OK` with the order ID on success.

## Signature Verification

The handler uses `EventUtility.ConstructEvent` from the Stripe .NET SDK to verify the webhook signature:

```csharp
stripeEvent = EventUtility.ConstructEvent(
    command.JsonPayload,
    command.SignatureHeader,
    stripeConfig.WebhookSecret,
    throwOnApiVersionMismatch: false);
```

If the signature is invalid, a `StripeException` is thrown and the handler returns:

```json
{
  "errors": {
    "InvalidSignature": ["Invalid webhook signature"]
  }
}
```

::: warning
The `WebhookSecret` (`whsec_...`) must match the secret configured in the Stripe Dashboard for the webhook endpoint. Each environment (DEV/PRO) has its own webhook secret stored in Azure Key Vault.
:::

## Handled Event Types

Eleven event types, in three groups (`Constants.StripeEventType` — `IsOrderEvent`,
`IsSubscriptionEvent`, `IsChargebackEvent`). Every host that runs the handler handles all eleven.

| Event Type | Constant | Action |
|------------|----------|--------|
| **Order** — found by the `OrderId` in the session's or intent's metadata | | |
| `checkout.session.completed` | `CompletedSession` | Web card payment settled: `PaymentStatus = Paid`. The order stays `New` ([ADR-0057](/decisions/adr-0057)). Receipt and push queued |
| `payment_intent.succeeded` | `PaymentIntentSucceeded` | Mobile card payment settled — handled exactly as above |
| `checkout.session.expired` | `ExpiredSession` | `PaymentStatus = Failed`, `OrderStatus.Cancelled` appended, applied credit returned |
| `payment_intent.canceled` | `PaymentIntentCanceled` | Handled exactly as an expired session |
| `payment_intent.payment_failed` | `PaymentIntentPaymentFailed` | Status left alone so the client can retry; the first decline on an order tells the administrators |
| **Subscription** (Cleansia Plus) — found by the Stripe subscription id | | |
| `customer.subscription.created` | `SubscriptionCreated` | Creates the local `UserMembership` — the only writer of that row for a web Plus checkout |
| `customer.subscription.updated` | `SubscriptionUpdated` | Mirrors Stripe's status and the current period onto the membership; a renewal is recorded here |
| `customer.subscription.deleted` | `SubscriptionDeleted` | Mirrors the cancellation |
| `invoice.payment_failed` | `InvoicePaymentFailed` | Marks the membership `PastDue` |
| **Chargeback** — found by the order's stored payment intent | | |
| `charge.dispute.created` | `ChargeDisputeCreated` | Links the order's open dispute, or creates an escalated `Chargeback` one; the administrators are told. A web card booking stores no payment intent, so its chargeback is not found — see [Cancellation, refund and dispute](/flows/cancellation-refund-dispute#dispute) |
| `charge.dispute.updated`, `charge.dispute.closed` | `ChargeDisputeUpdated`, `ChargeDisputeClosed` | Reflects Stripe's status onto the linked dispute |

All other event types are **ignored** and return `200 OK` with an empty response. So is a
subscription or chargeback event that resolves to no local row: it is logged and acknowledged, never
retried.

## Event Processing

### checkout.session.completed

`payment_intent.succeeded` takes the same path.

1. Extract `OrderId` from the session's (or intent's) metadata
2. Look up the order past the tenant filter and pin its tenant
3. **Cash check first:** an order a cleaner already settled in cash escalates a double-settlement
   dispute instead, and nothing else happens
4. **Idempotency check:** if the order is already `Paid` or `Refunded`, skip processing
5. Update `PaymentStatus` to `Paid`. `OrderStatus` is not touched: the order rests at `New` until a
   cleaner takes it ([ADR-0057](/decisions/adr-0057))
6. Stage the receipt (`generate-receipt`, key `receipt:{orderId}`), put on the wire only after the commit
7. Push `order.payment_confirmed` to a customer with an account; tell the preferred cleaner and the
   administrators that the order is now offerable

```csharp
order.UpdatePaymentStatus(PaymentStatus.Paid);

pending.Enqueue(
    QueueNames.GenerateReceipt,
    new QueueEnvelope<GenerateReceiptMessage>(
        MessageKeys.Receipt(orderId),
        order.TenantId,
        new GenerateReceiptMessage(orderId, language)),
    MessageKeys.Receipt(orderId));
```

### checkout.session.expired

`payment_intent.canceled` takes the same path.

1. Extract `OrderId` from the session's (or intent's) metadata
2. Look up the order past the tenant filter and pin its tenant
3. **Idempotency check:** if the order is already `Failed`, `Paid` or `Refunded`, skip processing
4. Update `PaymentStatus` to `Failed` and append `OrderStatus.Cancelled`
5. Return any credit the order applied at creation (keyed on the order, so it goes back once)
6. Push `order.cancelled` to a customer with an account

```csharp
order.UpdatePaymentStatus(PaymentStatus.Failed);
order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
await creditAccountRepository.ReturnUnpaidOrderCreditAsync(order, SystemActor, cancellationToken);
```

## Idempotency

Two layers handle Stripe's retry behavior:

- **Every event:** its id is written to `ProcessedStripeEvents` (unique) in the same commit as the
  handler's work. A redelivery finds the row and short-circuits; a parallel one loses on the index.
- **Completed / succeeded:** skips if `PaymentStatus` is `Paid` or `Refunded` (after the cash check)
- **Expired / canceled:** skips if `PaymentStatus` is `Failed`, `Paid` or `Refunded`

::: tip
Stripe retries webhook delivery for up to 3 days if the endpoint doesn't respond with `2xx`. The idempotency checks ensure duplicate deliveries don't cause issues.
:::

## Validation

Before processing, the handler validates:

1. `JsonPayload` is not empty
2. `SignatureHeader` is not empty
3. For order events: the `OrderId` in metadata references an existing order

## Stripe Dashboard Setup

**Two endpoints, two signing secrets.** Web and mobile take different Stripe paths — the web
channel mints a Checkout Session, mobile uses a PaymentIntent via PaymentSheet — so they emit
different event types and are hosted by different App Services. Each Stripe endpoint signs with
its **own** `whsec_`, and a payload signed by one will never verify against the other's secret.

| Channel | Endpoint URL | Events | GitHub Environment secret | Key Vault secret |
|---|---|---|---|---|
| Web | `https://api-cleansia-customer-<region>-<env>.azurewebsites.net/api/Payment/webhook` | `checkout.session.completed`, `checkout.session.expired`, and the seven account-wide events below | `STRIPE_WEBHOOK_SECRET_WEB` | `Stripe--WebhookSecret` |
| Mobile | `https://api-cleansia-customer-mobile-<region>-<env>.azurewebsites.net/api/Payment/webhook` | `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled` | `STRIPE_WEBHOOK_SECRET_MOBILE` | `Stripe--WebhookSecretMobile` |

**The account-wide events belong to no channel:** `customer.subscription.created`,
`customer.subscription.updated`, `customer.subscription.deleted`, `invoice.payment_failed`,
`charge.dispute.created`, `charge.dispute.updated`, `charge.dispute.closed`. Either host handles them,
so they need one endpoint; the table puts them on web. If both endpoints carry one, the second delivery
of the same event id is a no-op. **Left off both, nothing records them.** A web Plus checkout never
gets its `UserMembership` row, renewals and lapses are never mirrored, and no chargeback reaches a
dispute.

Steps, per environment (`dev-weu`, then `prod-weu`):

1. **Developers → Webhooks → Add endpoint**, once per row above, with that row's URL and events.
2. Reveal each endpoint's signing secret and set it as that row's **GitHub Environment secret**.
3. **Re-run the deploy.** The Key Vault push only happens inside a deploy run, so adding the
   GitHub secret on its own changes nothing that is live.

### Debugging `400 InvalidSignature`

The 400 means the HMAC over (raw body + `Stripe-Signature` timestamp + configured `whsec_`) did
not match, i.e. **the host that received the delivery is not holding the secret of the endpoint
that signed it.** In order of likelihood:

1. **`STRIPE_WEBHOOK_SECRET_MOBILE` was never set.** `deploy-azure.yml` falls back to
   `${STRIPE_WEBHOOK_SECRET_MOBILE:-$STRIPE_WEBHOOK_SECRET_WEB}`, so the *web* secret got written
   into `Stripe--WebhookSecretMobile` and every mobile delivery fails. Tell-tale: both Key Vault
   secrets share the same last 4 characters.
2. **The secret was set but no deploy has run since** — Key Vault still holds the fallback value.
3. **Set out-of-band with `az keyvault secret set` but the App Service was not restarted.** The
   Bicep KV reference is version-less, so the app keeps serving the cached old value for up to 24h.
   Always follow with `az webapp restart`. This is the usual "I already fixed it and it still fails".
4. **Cross-wired URLs** — the mobile endpoint points at the web host, or vice versa. Check the
   failing delivery's destination hostname in the Stripe dashboard.

Compare without printing secrets:

```bash
az keyvault secret show --vault-name kv-cleansia-weu-dev \
  --name Stripe--WebhookSecretMobile --query value -o tsv | tail -c 5
```

Replaying a fixed delivery is safe — the signature check runs before any database write, and the
processed-event guard makes a duplicate a no-op. Use **Resend** in the Stripe dashboard, or just
wait for Stripe's own 3-day retry.

## Error Responses

| Scenario | Status | Error |
|----------|--------|-------|
| Invalid signature | `400` | `InvalidSignature` |
| Missing OrderId in metadata | `400` | `OrderIdMissing` |
| Order not found | `400` | `OrderNotFound` |
| Unhandled event type | `200` | (empty -- acknowledged) |
| A chargeback (`charge.dispute.created`) on a web card booking, **guest** or account | `200` | (empty -- acknowledged and logged; the order is looked up by its stored payment intent, a Checkout Session order stores none, so no dispute is recorded; see [Cancellation, refund and dispute](/flows/cancellation-refund-dispute#dispute)) |
| The event's write lands on a company **frozen for archive** (a late chargeback, a settlement on an archived company's order) | `200` | (empty -- acknowledged; the verbatim body is recorded as a `DeadLetter` with source `stripe-webhook` and error `tenant.archived:<tenantId>`, and an Error is logged for operations — the write is never applied. Stripe is never asked to retry against a frozen company; see [ADR-0064](/decisions/adr-0064) D3) |
