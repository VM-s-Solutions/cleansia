# Partner Order Management

Order management is the core feature of the partner app, allowing cleaning partners to find available jobs, manage their assigned work, and document their progress. It is implemented in the `@cleansia-partner/orders` library.

## Architecture

The orders feature uses three facades:

- `OrdersFacade` -- Order list management (Available/My Orders tabs)
- `OrderDetailsFacade` -- Single order detail page with actions
- Dialog components for specific actions (Report Issue, Add Note, Complete Order)

## Order List

### Layout

The orders page displays two stacked tables (no tab switching required):

| Table | Description | Filter Logic |
|---|---|---|
| **Available Orders** (top) | Unassigned orders the partner can take | `hasAvailableSpots: true`, excludes current employee |
| **My Orders** (bottom) | Orders assigned to the current partner | `employeeId: currentEmployeeId` |

Each order row shows the **cleaning time** alongside the date for quick scheduling visibility.

### Available Orders

Shows orders that have available spots and are not already assigned to the current partner.

::: tip v4 Update
The Available Orders tab will soon show only `New` and `Pending` status orders. `Confirmed` orders are excluded because they already have a cleaner assigned.
:::

```typescript
const filter = new OrderFilter({
  orderStatuses: [OrderStatus.New, OrderStatus.Pending],
  hasAvailableSpots: true,
  excludeEmployeeId: employeeId,
});
```

### My Orders

Shows all orders assigned to the current partner, regardless of status.

### Sorting & Filtering

Both tabs support:
- **Sorting** via `SortDefinition[]` -- updates reset pagination to page 0
- **Filtering** via `OrderFilter` -- applied on top of tab-specific filters
- **Pagination** -- server-side with configurable offset and limit (default: 20)

## Order Detail Page

The order detail page (`/orders/:id`) shows comprehensive order information through decomposed sub-components:

| Component | Content |
|---|---|
| `OrderHeaderComponent` | Order number, status badge, action buttons |
| `OrderStatusComponent` | Current status with status history timeline |
| `OrderServiceDetailsComponent` | Service type, rooms, bathrooms, extras |
| `OrderPackagesComponent` | Selected packages |
| `OrderAdditionalServicesComponent` | Add-on services |
| `OrderExtrasComponent` | Extra options |
| `OrderCustomerInfoComponent` | Customer name, email, phone, address |
| `OrderPaymentInfoComponent` | Payment method, status, amount |
| `OrderPhotosComponent` | Before/after photo gallery with upload |

## Order Lifecycle: Take / Start / Complete

The partner order flow mirrors the Android app:

```
Available (Pending/Confirmed)
  → Take Order
    → Confirmed (assigned to partner)
      → Start Order
        → InProgress (work begins, timer starts)
          → Complete Order
            → Completed (work finished)
```

### Take Order

- Called via `OrderDetailsFacade.takeOrder(orderId, employeeId)` or `OrdersFacade.takeOrder(orderId)`
- A **Take Order** button is available on the order detail page for available orders
- Sends `TakeOrderCommand` to the API
- On success, the order transitions from `New`/`Pending` to `Confirmed` and is assigned to the partner
- The order moves from the "Available Orders" table to the "My Orders" table
- The order list is refreshed

#### Take Order Validations

Before an order can be taken, the following validations are enforced:

**Weekly order limit** (based on partner rating):

| Rating Range | Weekly Limit |
|---|---|
| 0 -- 3.5 | 3 orders/week |
| 3.6 -- 4.5 | 6 orders/week |
| 4.6+ | 10 orders/week |

**Time conflict detection:** The system checks for scheduling overlaps with the partner's existing orders. If the new order's cleaning time conflicts with an already-taken order, the take request is rejected.

**Profile and document checks:** The partner must have a complete profile and approved documents to take orders.

### Start Order

- Called via `OrderDetailsFacade.startOrder(orderId, employeeId)`
- Sends `StartOrderCommand` to the API
- Changes status to `InProgress`
- Records the start timestamp for elapsed time calculation

### Complete Order

Completion is handled directly from the order detail page (aligned with the Android app -- no dialog):

- `OrderDetailsFacade.completeOrder()` automatically calculates `actualMinutes` from the `InProgress` status timestamp
- Dispatches `completeOrder` NgRx action
- No manual time entry is required; the elapsed time is computed automatically

```typescript
// Elapsed time calculation (auto-computed on completion)
const inProgressEntry = order.statusHistory?.find(h => h.status.value === 3);
let actualMinutes = 0;
if (inProgressEntry) {
  const start = new Date(inProgressEntry.createdOn);
  actualMinutes = Math.max(1, Math.floor((Date.now() - start.getTime()) / 60000));
}
```

### Elapsed Timer

While an order is `InProgress`, an elapsed timer is displayed on the order detail page showing how long the cleaning has been running. The timer updates in real time based on the `InProgress` status timestamp.

## In-Progress Actions

While an order is `InProgress`, partners have access to Report Issue and Add Note dialogs directly from the order detail page. Notes and issues submitted via these dialogs are visible on the order detail page alongside other order information.

## Report Issue Dialog

Partners can report issues with an order via `OrderDetailsFacade.openReportIssueDialog()`:

1. Opens `ReportIssueDialogComponent` (PrimeNG DynamicDialog)
2. Partner enters a description of the issue
3. On submit, sends `ReportOrderIssueCommand` with `orderId`, `employeeId`, `description`
4. Order details are reloaded

## Add Note Dialog

Partners can add notes to an order via `OrderDetailsFacade.openAddNoteDialog()`:

1. Opens `AddNoteDialogComponent` (PrimeNG DynamicDialog)
2. Partner enters note content
3. On submit, sends `AddOrderNoteCommand` with `orderId`, `employeeId`, `content`
4. Order details are reloaded

## Photo Management

The `OrderPhotosComponent` provides before/after photo management with a staging workflow:

### Photo Types

| Type | Value | Description |
|---|---|---|
| `Before` | `1` | Photos taken before cleaning starts |
| `After` | `2` | Photos taken after cleaning is complete |

### Upload Flow

1. Partner clicks "Add Before Photos" or "Add After Photos"
2. Files are selected via native file input (`image/jpeg, image/jpg, image/png, image/webp`)
3. Files are validated (max 10MB, allowed types only)
4. Files are read as base64 and **staged** locally (shown with a yellow "Staged" badge)
5. Partner can review staged photos and remove unwanted ones
6. Clicking "Save Photos" sends `SaveOrderPhotosCommand` with all staged photos
7. Photos are uploaded to Azure Blob Storage and served via **SAS URLs**

::: tip SAS URLs
Photos are stored in Azure Blob Storage. The `blobUrl` returned by the API contains a time-limited SAS (Shared Access Signature) token for secure access. Photos are displayed directly from these URLs.
:::

### Photo Gallery

The `PhotoGalleryComponent` provides a full-screen gallery view for browsing uploaded and staged photos. It supports:
- Navigating between photos
- Viewing photo metadata (filename, capture date, employee name)
- Deleting uploaded photos (with confirmation dialog)
- Removing staged photos

### Delete Flow

1. Partner clicks delete on a photo
2. `DialogService.confirmTranslated()` shows a confirmation dialog
3. On confirm, `partnerClient.orderClient.deletePhoto(photoId, employeeId)` is called
4. Gallery is refreshed

## Receipt Download

Partners can download order receipts via `OrderDetailsFacade.downloadInvoice()`:
- Calls `partnerClient.orderClient.downloadReceipt(orderId)`
- Creates a blob URL and triggers a file download
- File is named `receipt_<orderNumber>.pdf`

## Print Support

`OrderDetailsFacade.printOrder()` triggers `window.print()` for printing the order detail page.
