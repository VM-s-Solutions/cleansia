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

### The status help

The orders page's help panel carries two legends, each row drawn with the same pill and tone as the
table's badge (`legendBadgeClass`). The order legend covers `Confirmed`, `InProgress`, `Completed` and
`Cancelled`; `New` and `OnTheWay`, which the badges can show, have no row. The payment legend covers
`Pending`, `Paid`, `Failed` and `Refunded`. There is no row for `OrderStatus.Pending`, because nothing
writes it.

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
| `OrderCustomerInfoComponent` | Customer name, email, phone, address — while the job is live and for 24 h after completion; on a past or cancelled job a note says the customer's details were removed ([Execution and completion](/flows/execution-and-completion#crew-access)) |
| `OrderPaymentInfoComponent` | Payment method, status, amount |
| `OrderPhotosComponent` | Before/after photo gallery with upload |

## Order Lifecycle: Take / Start / Complete

The partner order flow mirrors the Android and iOS apps:

```
Offerable (the work is not over, and it is Paid — or a one-off cash order; see /domain/offerability)
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
- **Every take opens the contract-for-work dialog first** (`components/work-contract-dialog/`,
  ADR-0068): it loads `GetWorkContractPreview` — the job facts and the contract text the order was
  booked under — and enables *Accept and take the job* only once *I have read and accept the contract
  for work for this job* is ticked
- Sends `TakeOrderCommand` with `acceptedWorkContractTextId` = the preview's `legalDocumentTextId`
  (the exact text row shown); a `contract.text_mismatch` re-fetches the preview, unticks and says the
  contract was updated; `legal.document_not_found` leaves the button disabled
- On success the partner is added to `AssignedEmployees` and the acceptance is recorded in the same
  commit. A `New` order also gets a `Confirmed` status track and the customer is notified; an order
  that was **already** `Confirmed` (a settled card booking) gets **no** status track at all — only
  the assignment row and the acceptance change
- The order moves from the "Available Orders" table to the "My Orders" table
- The order list is refreshed once

#### The contract on the detail

The detail pairs the caller's `assignedEmployees` entry with `workContractAcceptances` by seat id.
With a row: *You accepted the contract for work on {date}, version {version}* and **Read the
contract** (the dialog in `read` mode from `GetWorkContract(acceptanceId)` — the stored facts, the
accepted version, no tick). Without one while assigned — a seat an administrator formed — a banner
*Accept the contract for work before you start* opens the dialog in `accept` mode
(`AcceptWorkContract`); Start and Complete stay offered, and a `contract.acceptance_required` refusal
opens the same dialog. → [Business rules — the contract for work](/product/business-rules#work-contract)

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
snapshot, because throttling someone's earnings is a thing they may later ask about, and since
2026-09-28 it needs a written reason the partner sees on their profile. Taking a job past the cap fails
with `order.weekly_limit_reached`.

The count behind the cap is **status-aware**: only orders in a slot-blocking status count. A cancelled
order no longer consumes a week's allowance the way it did under the ladder — and neither does a
completed one, so the cap bounds what a cleaner holds *open at once* rather than what they get through in
a week.

→ [ADR-0053](/decisions/adr-0053)

**Time conflict detection:** the server checks for overlaps against the partner's live commitments
(`New`, `Pending`, `Confirmed`, `OnTheWay`, `InProgress` — terminal orders free the slot).

**Profile and approval checks:** the partner needs an address on file (`employee.profile_incomplete`)
and `ContractStatus == Approved` (`employee.not_approved`). A rejected, still-pending or terminated
cleaner is turned away. Document upload alone is not the gate. And while a partner document — the
framework contract, the self-billing agreement, the data-processing agreement — is in force and its
current version not accepted, the take is refused with `employee.legal_documents_not_accepted` and the
page points to the profile, where the documents are read and accepted (none is seeded yet).

**Taken off a job by an administrator:** the job detail shows the reason the administrator gave, read
from `GetMyAssignmentRemoval`; the notice itself carries only the order. A placement is an offer the
partner may drop without consequence → [Business rules](/product/business-rules#placement-is-an-offer).

**The contract for work** (ADR-0068) — a take without `acceptedWorkContractTextId` is
`contract.not_accepted`, judged before existence; a text row that is not of this order's document is
`contract.text_mismatch`, judged last. The dialog makes both unreachable from the web app; they are
what a stale or broken client sees.

### Seats

`RequiredEmployees = ceil(estimatedTime / 120)` and `MaxEmployees = RequiredEmployees + 0` —
**there is no spare seat** (`BookingPolicy.SpareSeatsPerOrder = 0`). A job needing a crew of two shows
two seats and no more, so a partner will never find an extra slot on a job that does not need them.

### Start Order

- Called via `OrderDetailsFacade.startOrder(orderId, employeeId)`
- Sends `StartOrderCommand` to the API
- Refused with `contract.acceptance_required` when the caller's seat has no accepted contract for
  work (a seat an admin formed) — the facade opens the contract dialog in `accept` mode; the same
  gate is on Complete
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

**From the orders list, completion opens a dialog** (`CompleteOrderDialogComponent`, via
`OrdersFacade.openCompleteOrderDialog`). It shows the estimated and actual minutes side by side, takes
the actual minutes (prefilled with the estimate) and **optional** notes — the form requires none,
like the server and the detail page, which sends an empty string. It passes no verdict: the *Delay* /
*On time* row is gone (2026-09-28), and the hint under the notes says only that they help improve the
estimates. Nothing in pay reads the minutes; the dashboard's time and productivity analytics do.

### Customer did not pay {#customer-did-not-pay}

**Owner ruling 2026-10-06.** When the customer of a cash job pays nothing at the door, the cleaner
reports it from the order detail instead of completing the job, and the job completes through the report
→ [Business rules — when the customer does not pay at the door](/product/business-rules#cash-not-paid).

- **Where it shows.** Beside *Mark cash collected*, only on a cash order in progress whose payment is
  still pending, to a cleaner on its crew (`canReportCashNotPaid` in `order-details.helpers.ts`, which
  mirrors the server's order gates; the contract for work is the server's to check). With no after photo
  the page warns as *Complete* does and sends nothing.
- **What it asks.** `OrderDetailsFacade.openReportCashNotPaidDialog()` opens the shared confirmation,
  marked dangerous: report this only if the customer paid nothing; the order is completed and its price
  becomes an amount the customer owes; the customer is told at once and cannot book again until it is
  paid; an administrator is alerted; the cleaner receives the reward as usual; only an administrator can
  undo it (`pages.order_details.cash_not_paid.*`, five locales).
- **What it sends.** `ReportCashNotPaidCommand { orderId }` through the regenerated
  `orderClient.reportCashNotPaid`. On success a notice says the order is completed and the customer owes
  the amount the server answered (`global.messages.orders.cash_not_paid_reported`), and the order is read
  again. A refusal is left to the translated interceptor notice and the order is read again;
  `contract.acceptance_required` opens the contract dialog in `accept` mode, as Start and Complete do.
- **Android and iOS** carry the same action on the order detail, shown once the after photos are in, with
  a system confirmation that names the amount the customer will owe (the price less any credit applied).
  It is not offered on the Active-list swipe, where cash collection is not offered either.

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
| `Before` | `1` | Photos taken before cleaning starts — offered while the job is `Confirmed`, `OnTheWay` or `InProgress` |
| `After` | `2` | Photos of the finished work — offered only while the job is `InProgress` |

The windows are the server's: an upload outside them is refused `order.photo.window_closed`, and a
delete once the job is `Completed` or `Cancelled` is refused `order.photo.locked`, so the page offers
neither there ([Photos](/flows/execution-and-completion#photo-windows)).

### Upload Flow

1. Partner clicks "Add Before Photos" or "Add After Photos"
2. Files are selected via native file input (`image/jpeg, image/jpg, image/png, image/webp`), which asks
   for the rear camera — a browser cannot guarantee that no copy stays on the device, so the mobile
   apps, which take job photos with the camera only, are the preferred place
3. Files are validated (max 10MB, allowed types only)
4. Files are read as base64 and **staged** locally (shown with a yellow "Staged" badge)
5. Partner can review staged photos and remove unwanted ones
6. Clicking "Save Photos" sends `SaveOrderPhotosCommand` with all staged photos
7. Photos are uploaded to Azure Blob Storage and served via **SAS URLs**

::: tip SAS URLs
Photos are stored in Azure Blob Storage. The `blobUrl` returned by the API contains a 15-minute SAS (Shared Access Signature) token for secure access. Photos are displayed directly from these URLs.
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
