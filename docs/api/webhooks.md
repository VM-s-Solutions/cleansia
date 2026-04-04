# Webhooks

Cleansia receives Stripe webhooks to confirm or cancel card payments. The webhook endpoint is unauthenticated but verified using Stripe's signature mechanism.

::: info Source Files
- Webhook handler: `src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs`
- Partner controller: `src/Cleansia.Web/Controllers/PaymentController.cs`
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

To configure the webhook in the Stripe Dashboard:

1. Go to **Developers > Webhooks**
2. Add endpoint URL: `https://api.cleansia.cz/api/Payment/webhook`
3. Select events: `checkout.session.completed`, `checkout.session.expired`
4. Copy the signing secret (`whsec_...`) to Azure Key Vault as `Stripe--WebhookSecret`

## Error Responses

| Scenario | Status | Error |
|----------|--------|-------|
| Invalid signature | `400` | `InvalidSignature` |
| Missing OrderId in metadata | `400` | `OrderIdMissing` |
| Order not found | `400` | `OrderNotFound` |
| Unhandled event type | `200` | (empty -- acknowledged) |
