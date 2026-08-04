# Payments

Cleansia supports two payment methods: **card** (via Stripe Checkout) and **cash**. Payment is initiated during order creation and confirmed either immediately (cash) or asynchronously via Stripe webhooks (card).

::: info Source Files
- Payment controllers: `src/Cleansia.Web.Partner/Controllers/PaymentController.cs`, `src/Cleansia.Web.Customer/Controllers/PaymentController.cs`, `src/Cleansia.Web.Mobile.Customer/Controllers/PaymentController.cs`
- Stripe client: `src/Cleansia.Infra.Clients/Stripe/StripeClient.cs`
- Stripe config: `src/Cleansia.Infra.Common/Configuration/StripeConfig.cs`
- Order creation handler: `src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs`
:::

## Payment Flow

### Card Payment (Stripe)

```
Client                    API                     Stripe
  |                        |                        |
  |-- CreateOrder -------->|                        |
  |   (paymentType: Card)  |-- CreateCheckout ----->|
  |                        |                        |
  |<-- stripeSessionId ----|                        |
  |                        |                        |
  |-- redirect to Stripe ->|                        |
  |                        |                        |
  |                        |<-- webhook ------------|
  |                        |   (session.completed)  |
  |                        |                        |
  |                        |-- Update order ------->|
  |                        |   PaymentStatus: Paid  |
  |                        |   OrderStatus: Confirmed
```

1. Client calls `POST /api/Order/CreateOrder` with `paymentType: 1` (Card)
2. Backend creates a Stripe Checkout Session and returns the URL
3. Client redirects to the Stripe Checkout URL
4. On successful payment, Stripe sends a `checkout.session.completed` webhook
5. Backend updates order to `PaymentStatus.Paid` and `OrderStatus.Confirmed`
6. Receipt generation is queued as a background job

### Cash Payment

```
Client                    API
  |                        |
  |-- CreateOrder -------->|
  |   (paymentType: Cash)  |
  |                        |-- OrderStatus: Confirmed
  |                        |-- Queue receipt generation
  |<-- order response -----|
```

Cash orders are immediately confirmed without any payment gateway interaction.

## Endpoints

### Create Payment (via CreateOrder)

Payment is initiated through the `CreateOrder` endpoint on both the Partner and Customer APIs.

```
POST /api/Payment/CreateOrder
POST /api/Order/CreateOrder
```

Both routes invoke the same `CreateOrder.Command` handler.

**Request body:** See [Orders - CreateOrder](/api/orders#createorder)

**Response:**

```json
{
  "id": "order-id",
  "confirmationCode": "ABC123",
  "stripeSessionId": "https://checkout.stripe.com/c/pay/cs_test_..."
}
```

| Field | Description |
|-------|-------------|
| `id` | The created order ID |
| `confirmationCode` | Human-readable confirmation code |
| `stripeSessionId` | Stripe Checkout URL (card) or `null` (cash) |

## Stripe Checkout Session

The `StripeClient.CreateCheckoutSessionAsync` method creates a Stripe Checkout Session with these parameters:

```csharp
var options = new SessionCreateOptions
{
    PaymentMethodTypes = ["card"],
    LineItems = [new SessionLineItemOptions
    {
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = order.Currency.Code.ToLower(),
            ProductData = new SessionLineItemPriceDataProductDataOptions
            {
                Name = $"Cleaning Order #{order.Id}"
            },
            UnitAmount = (long)(order.TotalPrice * 100)
        },
        Quantity = 1
    }],
    Mode = "payment",
    SuccessUrl = "{SuccessUrlBase}?session_id={CHECKOUT_SESSION_ID}&orderId={orderId}",
    CancelUrl = "{CancelUrlBase}?orderId={orderId}",
    Metadata = { { "OrderId", order.Id } }
};
```

| Parameter | Value |
|-----------|-------|
| Payment methods | Card only |
| Mode | One-time payment |
| Currency | Dynamic (from order's currency) |
| Amount | `TotalPrice * 100` (Stripe uses minor units) |
| Metadata | `OrderId` for webhook correlation |

## Payment Statuses

`PaymentStatus` is an axis of its own — it does **not** mirror `OrderStatus`. In particular, "card
payment initiated, waiting for the webhook" is `PaymentStatus.Pending` while `OrderStatus` is still
`New`; `OrderStatus.Pending` is dead and has no writer (ADR-0037 D5). See
[Orders → Order Lifecycle](/api/orders#order-lifecycle).

| Status | Value | Description |
|--------|-------|-------------|
| `Pending` | `1` | Order created, awaiting payment. **Every order starts here, cash included** — cash is not marked `Paid` at creation |
| `Paid` | `2` | Card webhook settled, or a recurring cash occurrence was confirmed. The same write appends `OrderStatus.Confirmed` |
| `Failed` | `3` | Payment failed, or `CleanupStalePendingOrders` gave up on an abandoned checkout |
| `Refunded` | `4` | Full refund issued |
| `Disputed` | `5` | Chargeback opened (`charge.dispute.*`) |
| `PartiallyRefunded` | `6` | Partial refund issued |

A cash order therefore stays `New` + `Pending` until a cleaner takes it — and it is still offerable,
because the offerability rule admits `New` + `Cash` explicitly.

## Stripe Configuration

Settings in `appsettings.json` under the `Stripe` section:

```json
{
  "Stripe": {
    "SecretKey": "sk_...",
    "PublishableKey": "pk_...",
    "WebhookSecret": "whsec_...",
    "WebhookUrl": "/api/payments/webhook",
    "SuccessUrlBase": "https://cleansia.cz/checkout/success",
    "CancelUrlBase": "https://cleansia.cz/checkout/cancel"
  }
}
```

::: warning
`SecretKey`, `PublishableKey`, and `WebhookSecret` must be stored in Azure Key Vault for deployed environments. Never commit these to source control.
:::

## Error Handling

If the Stripe Checkout Session creation fails, the API returns:

```json
{
  "errors": {
    "Card": ["payment.gateway_unavailable"]
  }
}
```

The order is **not** created if the Stripe session fails.
