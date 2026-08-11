# Webhooks

Cleansia receives Stripe webhooks to confirm or cancel card payments. The webhook endpoint is unauthenticated but verified using Stripe's signature mechanism.

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

| Event Type | Constant | Action |
|------------|----------|--------|
| `checkout.session.completed` | `Constants.StripeEventType.CompletedSession` | Mark order as Paid + Confirmed |
| `checkout.session.expired` | `Constants.StripeEventType.ExpiredSession` | Mark order as Failed + Cancelled |

All other event types are **ignored** and return `200 OK` with an empty response.

## Event Processing

### checkout.session.completed

1. Extract `OrderId` from session metadata
2. Look up the order in the database
3. **Idempotency check:** If order is already `Paid`, skip processing
4. Update `PaymentStatus` to `Paid`
5. Add `OrderStatus.Confirmed` to status history
6. Queue receipt generation via Azure Queue (`GenerateReceipt` message)

```csharp
order.UpdatePaymentStatus(PaymentStatus.Paid);
order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));

await queueClient.SendAsync(QueueNames.GenerateReceipt,
    new GenerateReceiptMessage(orderId, language), cancellationToken);
```

### checkout.session.expired

1. Extract `OrderId` from session metadata
2. Look up the order in the database
3. **Idempotency check:** If order is already `Failed` or `Paid`, skip processing
4. Update `PaymentStatus` to `Failed`
5. Add `OrderStatus.Cancelled` to status history

```csharp
order.UpdatePaymentStatus(PaymentStatus.Failed);
order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
```

## Idempotency

Both event handlers include idempotency checks to safely handle Stripe's retry behavior:

- **Completed:** Skips if `PaymentStatus == Paid`
- **Expired:** Skips if `PaymentStatus == Failed` or `PaymentStatus == Paid`

::: tip
Stripe retries webhook delivery for up to 3 days if the endpoint doesn't respond with `2xx`. The idempotency checks ensure duplicate deliveries don't cause issues.
:::

## Validation

Before processing, the handler validates:

1. `JsonPayload` is not empty
2. `SignatureHeader` is not empty
3. For handled event types: the `OrderId` in metadata references an existing order

## Stripe Dashboard Setup

**Two endpoints, two signing secrets.** Web and mobile take different Stripe paths — the web
channel mints a Checkout Session, mobile uses a PaymentIntent via PaymentSheet — so they emit
different event types and are hosted by different App Services. Each Stripe endpoint signs with
its **own** `whsec_`, and a payload signed by one will never verify against the other's secret.

| Channel | Endpoint URL | Events | GitHub Environment secret | Key Vault secret |
|---|---|---|---|---|
| Web | `https://api-cleansia-customer-<region>-<env>.azurewebsites.net/api/Payment/webhook` | `checkout.session.completed`, `checkout.session.expired` | `STRIPE_WEBHOOK_SECRET_WEB` | `Stripe--WebhookSecret` |
| Mobile | `https://api-cleansia-customer-mobile-<region>-<env>.azurewebsites.net/api/Payment/webhook` | `payment_intent.succeeded`, `payment_intent.payment_failed` | `STRIPE_WEBHOOK_SECRET_MOBILE` | `Stripe--WebhookSecretMobile` |

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
