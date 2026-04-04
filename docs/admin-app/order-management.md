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
- Order status (Pending, Confirmed, InProgress, Completed, Cancelled)
- Payment status
- Date range
- Customer name/email
- Assigned employee
- Service type

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
| `Pending` | 0 | Order created, awaiting partner assignment |
| `Confirmed` | 1 | Partner assigned, awaiting start |
| `InProgress` | 3 | Partner has started the cleaning |
| `Completed` | 4 | Cleaning finished |
| `Cancelled` | 5 | Order was cancelled |

## Payment Statuses

| Status | Description |
|---|---|
| `Pending` | Payment not yet received |
| `Paid` | Payment confirmed |
| `Failed` | Payment attempt failed |
| `Refunded` | Payment was refunded |
| `Disputed` | Payment is under dispute |
