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

```typescript
// OrdersFacade.loadAvailableOrders — the client's display refinement, not the boundary
const filter = new OrderFilter({
  orderStatuses: [OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed],
  hasAvailableSpots: true,
  excludeEmployeeId: employeeId,
  cleaningDateFrom: new Date(),
});
```

::: danger The client filter is not the security boundary
The server pins a non-admin caller's scope with `OrderSpecification.RestrictToEmployeeId`: results are
restricted to rows the caller is assigned to **or** rows that are both open and
[offerable](/api/orders#offerability-which-orders-a-cleaner-may-be-offered-and-may-take) (ADR-0037),
minus anything under a live preferred-cleaner hold for someone else (ADR-0036). The status list above
is a display refinement on top of that floor and can never widen it.

`OrderStatus.Pending` in that list is inert — nothing writes it. `Confirmed` is in the list because a
`Confirmed` order can still have a free seat; it does **not** mean nobody is assigned.
:::

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

The partner order flow mirrors the Android and iOS apps:

```
Offerable (New+Cash, or Confirmed+settled)
  → Take Order
    → assignment row added; New becomes Confirmed
      → Notify On The Way
        → OnTheWay
          → Start Order
            → InProgress (work begins, timer starts)
              → Complete Order
                → Completed (work finished)
```

### Take Order

- Called via `OrderDetailsFacade.takeOrder(orderId, employeeId)` or `OrdersFacade.takeOrder(orderId)`
- A **Take Order** button is available on the order detail page for available orders
- Sends `TakeOrderCommand` to the API
- On success the partner is added to `AssignedEmployees`. A `New` order also gets a `Confirmed` status
  track and the customer is notified; an order that was **already** `Confirmed` (a settled card
  booking) gets **no** status track at all — only the assignment row changes
- The order moves from the "Available Orders" table to the "My Orders" table
- The order list is refreshed

#### Take Order Validations

The backend runs one ordered `Cascade.Stop` chain and returns exactly one error — the first that
fails. The full ordered table lives in the
[API reference](/api/orders#takeorder-validations); the partner-visible highlights:

**Offerability** (ADR-0037) — the order must be `Confirmed`, or `New` for cash, *and* nothing
scheduled may still be able to retract it. A `New` card order awaiting its Stripe webhook is not
takeable → `order.not_takeable`.

**Preferred-cleaner hold** (ADR-0036) — if the customer named a cleaner and the hold has not expired,
the order's first seat is theirs. Everyone else sees `order.not_found`, identical to a genuinely
missing order.

**Weekly order limit** — **unlimited by default.** A cleaner takes as much work as they can find, and
nothing in the platform caps them unless an admin has said so for that one person.

The cap used to be a rating ladder — 3, 6 or 10 orders a week depending on the cleaner's score — applied
to everybody automatically. It was removed on 2026-08-22 (owner ruling): a new cleaner's rating starts
low because they have done nothing yet, so the ladder throttled hardest exactly the people who most
needed the work, and no admin ever chose it for anyone.

What replaced it is `Employee.WeeklyOrderLimit`, a nullable per-cleaner column. `null` means unlimited,
which is every cleaner today. An admin sets a number on one cleaner through
`PUT /api/AdminEmployee/{employeeId}/weekly-order-limit`; the action is audited with a real before/after
snapshot, because throttling someone's earnings is a thing they may later ask about. Taking a job past
the cap fails with `order.weekly_limit_reached`.

The count behind the cap is **status-aware**: only orders in a slot-blocking status count. A cancelled
order no longer consumes a week's allowance the way it did under the ladder — and neither does a
completed one, so the cap bounds what a cleaner holds *open at once* rather than what they get through in
a week.

→ [ADR-0053](/decisions/adr-0053)

**Time conflict detection:** the server checks for overlaps against the partner's live commitments
(`New`, `Pending`, `Confirmed`, `OnTheWay`, `InProgress` — terminal orders free the slot).

**Profile and approval checks:** the partner needs an address on file (`employee.profile_incomplete`)
and `ContractStatus == Approved` (`employee.not_approved`). A rejected, still-pending or terminated
cleaner is turned away. Document upload alone is not the gate.

### Seats

`RequiredEmployees = ceil(estimatedTime / 120)` and `MaxEmployees = RequiredEmployees + 0` —
**there is no spare seat** (`BookingPolicy.SpareSeatsPerOrder = 0`). A job needing a crew of two shows
two seats and no more, so a partner will never find an extra slot on a job that does not need them.

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
// Elapsed time calculation (auto-computed on completion) — order-details.facade.ts
// Compare against the OrderStatus enum, never a magic number: InProgress is 4, not 3.
const inProgressEntry = order.statusHistory?.find(
  (h) => h.status.value === OrderStatus.InProgress
);
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
