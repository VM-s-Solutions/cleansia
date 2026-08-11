# Order Tracking

The order tracking feature allows both authenticated and **unauthenticated** customers to check the status of their orders. It is implemented in the `TrackOrderComponent` within the `@cleansia-customer/orders` library.

## Route

```
/track-order    # Public -- no auth guard
```

This route is accessible without authentication, making it suitable for guest customers who placed orders without creating an account.

## How It Works

### Automatic Guest Order Lookup

When a guest (unauthenticated) user places an order, the `GuestOrderService` saves the `{ orderId, email }` pair to `localStorage`. When the user visits `/track-order`:

1. `GuestOrderService.getAll()` retrieves all saved guest orders
2. A **batch lookup** request is sent using `CustomerOrderClient.lookupBatch()`
3. Results are displayed as a list of recent orders

All API calls use `CustomerOrderClient` (not raw `HttpClient`) to ensure consistent error handling, authentication headers, and base URL resolution.

```typescript
const items = guestOrders.map(o =>
  new LookupOrderBatch_OrderLookupItem({
    orderId: o.orderId,
    email: o.email,
  })
);
this.orderClient.lookupBatch(new LookupOrderBatch_Query({ items }));
```

If no guest orders exist in localStorage, the manual lookup form is shown automatically.

### Manual Lookup

Users can also look up any order by entering:

| Field | Description |
|---|---|
| `orderNumber` | The display order number (e.g., `CLN-20260402-001`) |
| `email` | The email address used when placing the order |

The lookup calls `CustomerOrderClient.lookup(orderNumber, email)` which returns a `LookupOrder_Response` with full order details.

::: warning Rate Limiting
Lookup endpoints are rate-limited to **10 requests per minute** per IP address. Exceeding this limit returns a `429 Too Many Requests` response.
:::

### URL Query Parameters

The tracking page supports direct linking with pre-filled fields, and **auto-fills from email link query params**:

```
/track-order?orderNumber=CLN-20260402-001&email=customer@example.com
```

When both query parameters are present, the lookup is triggered automatically on page load. This is commonly used in order confirmation emails to provide one-click tracking.

## Order Status Display

Each order displays a status timeline using PrimeNG's `Timeline` component. Status values use the
`OrderStatus` enum, mapped by the shared `OrderStatusIconPipe` / `OrderStatusSeverityPipe`
(`libs/shared/pipes/src/lib/order-status/`) — one source of truth across customer, partner and admin:

| Status | Value | Icon | Severity |
|---|---|---|---|
| `New` | `0` | `pi pi-circle` (default arm) | `info` (default arm) |
| `Pending` | `1` | `pi pi-clock` | `warn` |
| `Confirmed` | `2` | `pi pi-check` | `info` |
| `OnTheWay` | `3` | `pi pi-send` | `info` |
| `InProgress` | `4` | `pi pi-spin pi-spinner` | `info` |
| `Completed` | `5` | `pi pi-check-circle` | `success` |
| `Cancelled` | `6` | `pi pi-times-circle` | `danger` |

::: warning `New` is the status a freshly booked order actually has
Both pipes handle `New` only through their `default` arm, and nothing in production ever writes
`Pending` (ADR-0037 D5) — so the `pi pi-clock` / `warn` row is effectively dead while the state a
customer sees right after booking falls through to the generic icon.
:::

## Payment Status

Payment status is shown alongside order status. **This is the axis that carries "card payment
initiated, waiting for the webhook"** — a card order sits at `OrderStatus.New` with
`PaymentStatus.Pending` until Stripe confirms.

| Payment Status | Value | Severity |
|---|---|---|
| `Pending` | `1` | `warn` |
| `Paid` | `2` | `success` |
| `Failed` | `3` | `danger` |
| `Refunded` | `4` | `info` |
| `Disputed` | `5` | `danger` |
| `PartiallyRefunded` | `6` | — |

## Data Displayed

For each tracked order, the following information is shown:

- Order number
- Order status (with PrimeNG `Tag`)
- Payment status
- Cleaning date/time
- Total price (formatted per order currency)
- Status timeline (history of status changes)
- Service details

::: tip
Prices are formatted using the order's currency code with `Intl.NumberFormat`. The locale is derived from the current translation language (`cs` maps to `cs-CZ`, `en` to `en-US`).
:::

## Guest Order Storage

The `GuestOrderService` manages guest order tracking data:

```typescript
// Save after order creation
guestOrderService.save(orderId, email);

// Retrieve all saved orders
guestOrderService.getAll(); // returns { orderId, email }[]

// Clear all (e.g., after login)
guestOrderService.clear();
```

::: warning
Guest order data is stored in `localStorage` and is device-specific. If the user clears browser data or uses a different device, they must use the manual lookup form with their order number and email.
:::

## Error Handling

- If the batch lookup fails, the manual lookup form is shown as a fallback
- If a manual lookup fails (404), an error message is displayed: "Order not found"
- Network errors show a generic error state

## Navigation

From the tracking page, users can:
- Navigate to the order wizard to place a new order
- Toggle between the guest order list and manual lookup form
