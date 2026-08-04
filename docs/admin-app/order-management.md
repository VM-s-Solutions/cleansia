# Admin Order Management

The admin order management feature provides administrators with oversight of all orders in the system, including the ability to view details, manage disputes, reassign orders, and handle refunds. It is implemented in the `@cleansia/admin-features/order-management` library.

## Architecture

- `OrderManagementFacade` -- Order list with filtering, sorting, and pagination
- `OrderDetailComponent` -- Order detail page with admin-specific actions
- `AdminOrderPhotosComponent` -- Photo gallery with admin view capabilities
- `AdminPhotoGalleryComponent` -- Full-screen photo viewer

## Order List

Route: `/order-management`

The order list displays all orders across the platform with:

| Column | Description |
|---|---|
| Order Number | Display order number |
| Customer | Customer name and email |
| Assigned Employee | Partner assigned to the order |
| Service | Cleaning service type |
| Status | Current order status |
| Payment Status | Payment state |
| Cleaning Date | Scheduled date/time |
| Total Price | Order amount |
| Created Date | When the order was placed |

### Filtering

Admins can filter orders by:
- Order status
- Payment status
- Date range
- Customer name/email
- Assigned employee
- Service type

::: warning The status dropdown does not cover the whole enum
`OrderManagementFacade.orderStatusOptions` offers **Pending, Confirmed, InProgress, Completed,
Cancelled**. `Pending` is dead so that option matches nothing, and `New` and `OnTheWay` have no
option at all — the orders sitting in those states (every card order awaiting its webhook, every
untaken cash order, every cleaner en route) are reachable only by clearing the status filter.
:::

### Sorting & Pagination

- Server-side sorting on any column
- Server-side pagination with configurable page size

## Order Detail

Route: `/order-management/:id`

The admin order detail page provides a comprehensive view of a single order with admin-specific capabilities that go beyond what partners can see.

### Information Displayed

| Section | Content |
|---|---|
| Order Header | Order number, status, creation date |
| Customer Info | Name, email, phone, address |
| Service Details | Selected services, packages, rooms, bathrooms |
| Employee Info | Assigned partner details |
| Payment Info | Method, status, amount, Stripe references |
| Status History | Timeline of all status changes |
| Notes | All notes added by partners and admins |
| Photos | Before/after photos from partners |

## Dispute Resolution

When a customer or partner raises a dispute, admins can:

1. View the dispute details and associated order
2. Review before/after photos and partner notes
3. Investigate the issue
4. Resolve the dispute by:
   - Siding with the customer (potential refund)
   - Siding with the partner (no action needed)
   - Finding a compromise

::: info
Disputes are linked to specific orders and contain a description of the issue. The admin can view the full order history, including status changes, notes, and photos, to make an informed decision.
:::

## Order Reassignment

Admins can reassign orders from one partner to another. This is useful when:
- A partner becomes unavailable
- A partner requests to be removed from an order
- An issue requires a different partner to handle the job

The reassignment process:
1. Select a new employee from the available partners
2. Confirm the reassignment
3. The order status and assignment are updated
4. Both the original and new partners are notified

## Refunds

Admins can initiate refunds for orders with card payments:

1. Navigate to the order detail page
2. Verify the payment status is `Paid`
3. Initiate refund (full or partial)
4. The refund is processed through the payment provider
5. Payment status is updated to `Refunded`

::: warning
Refunds for Stripe payments are processed asynchronously. The payment status may not update immediately. Cash payment refunds must be handled outside the system.
:::

## Photo Management

The admin order detail includes photo viewing capabilities via `AdminOrderPhotosComponent`:

- View all before/after photos uploaded by partners
- Photos are displayed with metadata (filename, capture date, employee name)
- Full-screen gallery view via `AdminPhotoGalleryComponent`
- Photos are served via Azure Blob Storage SAS URLs

Unlike the partner view, the admin photo component is read-only -- admins cannot upload or delete photos.

## Order Statuses

| Status | Value | Description |
|---|---|---|
| `New` | 0 | Order created. Every order starts here, cash and card alike |
| `Pending` | 1 | **Dead — nothing writes it** (ADR-0037 D5). Legacy rows may still hold it |
| `Confirmed` | 2 | Cleaner took it, OR the Stripe webhook settled a card payment, OR a recurring cash occurrence was confirmed, OR an admin overrode the status |
| `OnTheWay` | 3 | Cleaner is en route |
| `InProgress` | 4 | Cleaner has started the cleaning |
| `Completed` | 5 | Cleaning finished |
| `Cancelled` | 6 | Order was cancelled |

::: warning `Confirmed` does not mean a partner is assigned
Four paths write it and only one involves a cleaner. To tell whether a cleaner is actually on the job,
read the assignment rows, not the status. See
[the API reference](/api/orders#order-lifecycle) for the full two-axis model.
:::

## Payment Statuses

Order state is **two axes** — this one carries "where is the money", including the *card payment
initiated, waiting for the webhook* state that `OrderStatus.Pending` never tracked.

| Status | Value | Description |
|---|---|---|
| `Pending` | 1 | Payment not yet received |
| `Paid` | 2 | Payment confirmed |
| `Failed` | 3 | Payment attempt failed |
| `Refunded` | 4 | Payment was refunded |
| `Disputed` | 5 | Payment is under dispute |
| `PartiallyRefunded` | 6 | Part of the payment was refunded |

| Payment type | Value |
|---|---|
| `Cash` | 1 |
| `Card` | 2 |

## Order Status Override

`AdminOverrideOrderStatus` moves an order **strictly forward** along
`New → Confirmed → OnTheWay → InProgress → Completed`. Same-state, backward and off-lifecycle targets
are refused (`order.invalid_status_transition`), as is any move out of a terminal state
(`order.already_completed` / `order.already_cancelled`). Cancellation is not available here — it is
`AdminCancelOrder`, which carries the refund seam.

`OrderStatus.Pending` is refused as a target: it is dead, and this generic writer is the only way a
new `Pending` row could appear. It stays in the handler's rank array so legacy rows holding it can
still be ranked and moved forward.

Every override is audited (`order.status.override`, marked sensitive) and, for targets that map to a
Live Activity event, pushes a state-card update.
