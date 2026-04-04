# Checkout & Payment

The checkout system handles payment processing via **Stripe** for card payments and direct order creation for cash payments. The checkout library (`@cleansia-customer/checkout`) provides the post-payment result pages.

## Payment Flows

### Card Payment (Stripe)

```
Order Wizard → submitOrder() → paymentClient.createOrder()
  → Backend creates Stripe Checkout Session
  → Response contains stripeSessionId (Stripe URL)
  → Browser redirects to Stripe Checkout
  → Customer completes payment on Stripe
  → Stripe redirects to /checkout/success or /checkout/cancel
```

1. The `OrderWizardFacade.submitOrder()` calls `customerClient.paymentClient.createOrder(command)` when `paymentType === PaymentType.Card`
2. The backend creates a Stripe Checkout Session and returns the session URL in the `stripeSessionId` field
3. The browser is redirected to Stripe via `window.location.href = response.stripeSessionId`
4. After payment, Stripe redirects back to:
   - **Success**: `/checkout/success?type=card`
   - **Cancel**: `/checkout/cancel`

::: info
The Stripe integration uses **Stripe Checkout** (hosted payment page), not Stripe Elements. This means the frontend never handles raw card data -- all PCI compliance is handled by Stripe.
:::

### Cash Payment

```
Order Wizard → submitOrder() → orderClient.createOrder()
  → Backend creates order with PaymentType.Cash
  → Navigate to /checkout/success?type=cash
```

1. The `OrderWizardFacade.submitOrder()` calls `customerClient.orderClient.createOrder(command)` when `paymentType === PaymentType.Cash`
2. On success, the router navigates to `/checkout/success?type=cash`

## Guest Order Tracking

Regardless of payment method, when an order is created successfully:

```typescript
if (response.id) {
  this.guestOrderService.save(response.id, data.customerEmail);
}
```

The `GuestOrderService` stores `{ orderId, email }` pairs in `localStorage`, allowing unauthenticated users to track their orders later via the `/track-order` page.

## Checkout Routes

```typescript
export const checkoutRoutes: Route[] = [
  { path: 'success', component: CheckoutSuccessComponent },
  { path: 'cancel',  component: CheckoutCancelComponent },
];
```

### Success Page (`/checkout/success`)

The success page adapts its content based on the `type` query parameter:

| Parameter | Behavior |
|---|---|
| `?type=card` | Shows card payment confirmation message |
| `?type=cash` | Shows cash payment confirmation message |

The page dynamically sets the browser title based on payment type and provides navigation:

- **Authenticated users**: Link to `/orders` (My Orders)
- **Guest users**: Link to `/track-order` (Track Order)

```typescript
ordersRoute = this.authService.isLoggedIn()
  ? '/' + CleansiaCustomerRoute.ORDERS
  : '/' + CleansiaCustomerRoute.TRACK_ORDER;
```

### Cancel Page (`/checkout/cancel`)

Shown when the user cancels the Stripe payment. Provides a link back to the home page or to retry the order.

## Payment Status Tracking

Orders have a `PaymentStatus` enum tracked throughout their lifecycle:

| Status | Description |
|---|---|
| `Pending` | Payment not yet received |
| `Paid` | Payment confirmed |
| `Failed` | Payment attempt failed |
| `Refunded` | Payment was refunded |
| `Disputed` | Payment is under dispute |

::: warning
The cancel page does not automatically retry or void the order. If a user cancels at Stripe, the order remains in `Pending` payment status until it expires or is manually handled.
:::

## Components

Both checkout components are standalone and use:

- `CleansiaDynamicBackgroundComponent` -- Animated background
- `TranslatePipe` -- i18n support
- `RouterLink` -- Navigation
- `ChangeDetectionStrategy.OnPush` -- Performance optimization
