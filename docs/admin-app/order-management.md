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
| Total Price | Order amount, labelled with the order's own currency symbol |
| Created Date | When the order was placed |

### Filtering

Admins can filter orders by:
- Order status
- Payment status
- Date range
- Customer name/email
- Assigned employee
- Service type
- Currency (`OrderFilter.CurrencyId` on the wire — one currency id, no existence check; an unknown id is an empty page)

### Money across currencies {#money-across-currencies}

Every order is in the currency of its service address's country, so a page of orders can hold CZK
and EUR rows side by side once a second market is open. Two things follow. The **currency filter**
pins the page to one currency, and while it is set a sort on Total Price is a plain price order. With
**no currency filter**, a click on Total Price sorts *within* currency: the server prepends
`CurrencyId` ascending to the requested sort (`SortMapper.WithinCurrencyWhenSortedBy`), so the rows
arrive grouped by currency and ordered by price inside each group, rather than filing 150 EUR below
3 000 CZK as if the numbers were comparable. The column stays sortable either way — a click that
silently does nothing is the failure the admin cannot see. The invoice list does the same on Total
Amount with `EmployeeInvoiceFilter.CurrencyId`. → [Reporting](./reporting#currency)

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
| Employee Info | Assigned partner details — and, per crew member, whether the **contract for work** is accepted: *accepted {date}, v{version}* or *contract pending*. **Read** on an accepted one opens the accepted text with the job facts frozen at acceptance and, for an Administrator, the accepted text row's SHA-256 (the hash a dispute cites) → [Business rules — the contract for work](/product/business-rules#work-contract) |
| Payment Info | Method, status, amount, Stripe references. On a cancelled order, **Cancellation fee** (the rate the cancellation applied, in the session language's number format — a free 0 % cancellation still shows) and **Fee still owed**: the whole fee on an order that took no payment (`Pending` or `Failed`), which nothing collects yet, and zero where the card charge covered it. A confirmed recurring cash occurrence stays `Pending` until the cash is recorded, so it owes its fee like any cash booking. Only an administrator's order detail carries the two figures → [Business rules — cancellation](/product/business-rules#cancellation). On a cash order the cleaner or an administrator recorded, **Cash collected by**, **at** and **amount** (`cashCollectedByName`, `cashCollectedAt`, `cashCollectedAmount` — administrator reads only) |
| Status History | Timeline of all status changes |
| Notes | All notes added by partners and admins |
| Photos | Before/after photos from partners |

### Cancel as a no-show {#cancel-as-no-show}

When the assigned cleaner did not come, **Cancel as a no-show** confirms it (owner ruling 2026-09-28;
`POST api/AdminOrder/cancel-no-show {orderId}`, Support and above, audited `order.cancel.no_show`). The
question reaches the console as `admin.order.cleaner_not_started` — raised by the reminder sweep 30
minutes after the start of a job nobody started, or by the customer's report that the cleaner did not
arrive — and the button is the answer. It cancels with the unfilled sweep's own remedy: no fee, the whole
card refund, the customer's applied credit back, the apology credit, the reason *no cleaner was
available*, and the push that says what happened to the money; it tells the assigned cleaners, ends the
live activity, releases the express waiver, revokes the booking's loyalty points and closes an open
*service not provided* dispute. The response says what moved: `refundedAmount`, `refundPending` (a card
refund owed and not through — the hourly re-drive owns it) and `apologyCredit`; the toast states only
that this action made no card refund when none went through. It is refused before the start
(`order.start_time_not_reached`), once a cleaner has started (`order.cleaner_already_started` — the
cleaner did arrive; the report stays a dispute), and on a completed or cancelled order. One of two
cleaners missing is not a no-show here; settle it through the dispute.
→ [Business rules — when the cleaner no-shows](/product/business-rules#when-the-cleaner-cancels-or-no-shows)

### Record cash received {#record-cash-received}

When the cleaner could not record a cash handover themselves, an administrator records it (owner ruling
2026-09-28; `POST api/AdminOrder/record-cash {orderId, employeeId, receivedAt, amount}`, the
status-override permission, audited `order.cash.record`): which **assigned** cleaner took the cash, when
(not in the future — `order.cash_received_at_in_future` — and not before the clean could begin —
`order.cash_received_at_before_clean`) and how much (above zero, whole cents — `order.cash_amount_invalid`).
Only on a cash order in progress or completed whose money is still owed. The order is then `Paid`
exactly as the cleaner's own record would make it, so the cleaner can complete it; on an order already
completed the cash receipt is issued at once.
→ [Business rules — cash handover](/product/business-rules#cash-handover)

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

**The customer chose how a justified complaint is settled** (owner ruling 2026-09-28): the dispute
detail shows *Card refund* — the default — or *Credit*, and the resolve dialog shows the choice. The
administrator decides the amount only. With *Credit* the amount is issued as credit in the order's
currency and recorded as **Returned as credit**, bounded by what the order has not already given back
(`dispute.invalid_refund_amount`); nothing moves on the card. *Issue credit* no longer offers a
*Dispute settlement* reason, and the server refuses it (`credit.dispute_settlement_not_issuable`).

**Charging the cleaner is a finding of fault, not a side effect.** The resolve dialog can charge a named
crew member an amount with a written reason (`chargeToCleaner`), deducted from their pay on this order,
linked to the dispute and shown to them with the reason. It is refused when that pay row is missing,
already invoiced, already charged for a dispute or smaller than the charge
(`dispute.cleaner_charge_not_chargeable`); an invoiced row takes the ordinary invoice deduction instead.
→ [Business rules — dispute settlement](/product/business-rules#dispute-settlement)

**A resolution with a refund moves the money before it is recorded.** Resolving with an amount above
zero issues that refund first: the card share through Stripe, and on an order settled partly from
customer credit, the credit share back to the customer's balance. The dispute becomes *Resolved* only
when the refund succeeds, and its detail then shows three figures: **Refund requested** (the amount
the resolution named), **Refunded to card** and **Returned as credit** (what the refund moved, in the
dispute's currency). A leg that moved nothing shows zero; a dispute resolved without a refund, or
before the two legs were recorded, shows neither. The dispute list's refund column is still the
requested amount. A refused refund shows its error and leaves the dispute open:
`refund.failed`, `refund.order_not_refundable` (a cash booking has no card charge) or
`refund.nothing_refundable`. Resolving again re-drives the first attempt's refund, never a second one,
and at the first attempt's amount even if the new resolution names another. A resolution with no
amount moves no money. → [Cancellation, refund and dispute](/flows/cancellation-refund-dispute#dispute)

A dispute that names no account shows the booking's own customer name and e-mail, read off the order.
A bank chargeback reaches the console on every card booking, **guest** ones included, as an escalated
*Chargeback* dispute (or linked to the order's open one). A chargeback that matches no order cannot be
a dispute; it arrives by e-mail as `admin.dispute.chargeback_unmatched`, with the amount and the Stripe
dispute id to find it by in the Stripe dashboard, and as a notifications-page row carrying the same
two figures.
→ [Cancellation, refund and dispute](/flows/cancellation-refund-dispute#dispute)

## Order Reassignment

Admins can reassign orders from one partner to another. This is useful when:
- A partner becomes unavailable
- A partner requests to be removed from an order
- An issue requires a different partner to handle the job

The reassignment process:
1. Select a new partner from the available partners
2. When someone is taken off the job, write the **reason** — required (`order.reassign.removal_reason_required`,
   at most 500 characters); it goes on the audit row, and the removed partner reads it on the job
3. Confirm the reassignment
4. The order status and assignment are updated
5. Both the original and new partners are notified; the removed partner's notice carries only the order

A placement is an **offer** the partner may decline — drop — without consequence (owner ruling
2026-09-28) → [Business rules — placement is an offer](/product/business-rules#placement-is-an-offer).

**The cleaner placed must be able to do the job.** `AdminReassignOrder` refuses, in this order:

| Refusal | Key |
|---|---|
| No such cleaner | `employee.not_found` |
| The cleaner is not active and `Approved` — pending, rejected, terminated, or deactivated | `order.reassign.employee_not_approved` |
| The cleaner's work country is not the order's market, or they have none | `order.reassign.employee_other_market` |
| The cleaner has a live job that overlaps this one | `order.reassign.employee_busy` |
| The cleaner has not accepted a cleaner document in force (none is seeded yet) | `employee.legal_documents_not_accepted` |

A cleaner already on this order is not refused as busy; the handler answers that they are already
assigned. The weekly cap, the preferred-cleaner hold and profile completeness, which a cleaner's own
take checks, are not checked on a placement.

**A reassignment writes no contract acceptance** (ADR-0068 D3): an administrator cannot accept the
contract for work on the cleaner's behalf, so the placed cleaner's seat reads *contract pending* on
this page until they accept it from their app — the job detail shows them a banner, and Start and
Complete refuse them with `contract.acceptance_required` until they do. The seat they replaced keeps
its acceptance row as history.

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
| `Confirmed` | 2 | A cleaner took it or an admin placed one ([ADR-0057](/decisions/adr-0057)); an admin override may set it only on an order with a crew. Paying and confirming a recurring occurrence move the payment axis, never this one |
| `OnTheWay` | 3 | Cleaner is en route |
| `InProgress` | 4 | Cleaner has started the cleaning |
| `Completed` | 5 | Cleaning finished |
| `Cancelled` | 6 | Order was cancelled |

::: warning `Confirmed` is a summary of the crew, not the crew
Two releases racing can leave `Confirmed` with nobody on it. To tell whether a cleaner is actually on
the job, read the assignment rows, not the status. See
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

**`Confirmed` is refused as a target on an order with nobody assigned** (`order.status.confirmed_needs_crew`,
[ADR-0067](/decisions/adr-0067)). `Confirmed` means a cleaner took the job, and a release that empties
the crew walks it back to `New` — so this was the one door an administrator could open onto that state
with a click. An administrator who wants a cleaner on the order reassigns, which writes `Confirmed`
itself. The other forward moves stay open on an unstaffed order: `New → OnTheWay / InProgress /
Completed` are the *"the cleaner is there but never tapped"* repairs, about the work's state rather
than the crew's, and they are the administrator's own audited act — an unstaffed `OnTheWay` produced
this way is one no sweep cancels out from under them.

**An override to `Completed` stamps `CompletedAt`.** The override does not run the completion
pipeline (pay, fiscal — that is `CompleteOrder`'s), but the revenue report reads `CompletedAt`, so an
override that completes the order has to date it or the order is revenue of no month; the first stamp
wins. A sale already settled in cash with no receipt gets its receipt here, because a cash receipt is
issued at completion; uncollected cash gets none until *Record cash received*.

**Completing without an *after* photo needs a reason** (owner ruling 2026-09-28). It is the only way to
close an order stuck in progress, so it stays — but `Completed` on an order with no *after* photo is
refused without a written `reason` (`order.status.force_complete_reason_required`, at most 500
characters), which is kept on the audit row.

Every override is audited (`order.status.override`, marked sensitive) and, for targets that map to a
Live Activity event, pushes a state-card update.
