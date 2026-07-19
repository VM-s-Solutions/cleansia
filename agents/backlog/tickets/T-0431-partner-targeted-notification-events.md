---
id: T-0431
title: "Partner-targeted notification events — assignment, accepted-job cancellation, invoice-ready"
status: proposed
size: M
owner: analyst
created: 2026-07-18
updated: 2026-07-18
depends_on: [T-0393]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: T-0393 Q-FEED-02 (default taken: dedicated follow-up rather than inventing events inside feed v1)
---

> Feed v1 ships partner = `order.new_available` only, because that is the only partner-targeted
> dispatch that exists. This ticket adds the missing partner events end-to-end (catalog key +
> producer + push loc-keys ×5 in both partner apps + feed row via the shared notify seam):
> **accepted-job cancellation is the highest-impact candidate — a cleaner currently learns nothing
> when a job they accepted is cancelled.** Also: assignment confirmation, invoice-ready.
> Analyst decides the exact keyset + copy first (client-first rule for the APNs display map).

## Status log
- 2026-07-18 — filed by the PM as the Q-FEED-02 default disposition.
- 2026-07-18 — analyst panel (author → challenger → lead) decided the v1 keyset + copy. See below.

## Event design (analyst panel)

### Decision (lead ruling, consensus)

**v1 ships two partner events; assignment-confirmation is cut.**

| # | event_key | category | trigger | recipient | args (loc / data) | feed | push |
|---|---|---|---|---|---|---|---|
| 1 | `order.assignment_cancelled` | **null (non-mutable)** | `CancelOrder` **and** `AdminCancelOrder`, when `order.AssignedEmployees.Any()` at cancel time | **each** assigned employee's `Employee.UserId` (skip null-UserId legacy rows) | loc: `orderNumber` · data: `orderId` | Partner | yes |
| 2 | `payroll.invoice_ready` | **null (non-mutable)** | `GenerateInvoice.Handler` on `result.IsSuccess` (invoice created) | the invoice's `Employee.UserId` (skip null) | loc: *(argless)* · data: `invoiceId` | Partner | yes (see OPEN-2) |

- **CUT: assignment-confirmation.** No notify-worthy trigger — assignment is self-service pull
  (`TakeOrder`, verified); the cleaner is the actor and already gets the synchronous command
  `Response`. There is **no admin "assign cleaner to order" handler** anywhere in AppServices
  (grep: no `AssignEmployee`/`AssignOrder`/`AssignCleaner`), so no third party ever assigns a cleaner
  without the cleaner's own action. Notifying someone of an action they just performed is noise.

Both new keys go **only** into `NotificationFeedEventKeys.Partner` — never `Customer`. Neither is
added to `NotificationEventCatalog.GetCategoryFor` (the `_ => null` default gives non-mutable
delivery — see the category note below). Both fit ADR-0025's D3 `{orderNumber, count}` loc-args
allowlist with **no allowlist change** (event 1 uses `orderNumber`; event 2 is argless), so
`TC-PUSH-APNS-5` stays green.

### AUTHOR — the case

**Event 1 — `order.assignment_cancelled` (the flagship, highest impact).**
- *The gap, verified against source:* `CancelOrder.Handler` (customer path) and `AdminCancelOrder.Handler`
  (admin path) each notify **only the customer** (`order.refunded`, and only when a refund initiates).
  Neither loads or touches `order.AssignedEmployees`. A cleaner who accepted a job and planned their
  day around it learns **nothing** when the customer or an admin cancels it — they show up to a locked
  door. This is the single highest-impact partner-notification gap.
- *Reachable states:* both handlers block `InProgress`, `Completed`, `Cancelled`; cancel is only legal
  from `New`/`Pending`/`Confirmed`/`OnTheWay`. A cleaner is assigned (via `TakeOrder`) only after the
  order is `Confirmed` or later. So the cancel-with-an-assignment window is exactly **`Confirmed` and
  `OnTheWay`** — precisely "a job they accepted, or are en route to."
- *Why a distinct key (not reuse `order.cancelled`):* `order.cancelled` is a **customer** feed key
  (`NotificationFeedEventKeys.Customer`). Dispatching it to a partner `UserId` would (a) write a feed
  row that a dual-role user's **customer** app would surface (keysets are deliberately disjoint so the
  two hosts can't read each other's rows — reuse defeats that), and (b) bind the push to the
  customer-worded `OrderCancelled` toggle. A dedicated partner key keeps keysets disjoint, renders
  partner-voiced copy, and is category-independent. (Note: the partner Android `NotificationTemplates.kt`
  already carries a *dead* `order.cancelled` template left by T-0393 "to slot in" — that template is
  **not** reused; it stays dead or is removed as housekeeping, out of scope here.)
- *Recipient:* an order can have multiple spots (`AssignedEmployees` is a collection) — notify **each**
  assigned employee's `Employee.UserId`. Guard `!string.IsNullOrEmpty(employee.UserId)` (the digest
  service documents that some legacy employee rows have no `UserId`).
- *Producers:* `CancelOrder` + `AdminCancelOrder` **only**, gated on `AssignedEmployees.Any()`.
  `AutoCancelStaleRecurringOrders` (Pending recurring templates) and `HandlePaymentNotification`
  (expired checkout) act **only on still-`Pending`, never-taken orders** → zero assigned employees →
  they naturally produce no partner notification. They are excluded by the same `Any()` gate, not by a
  special case.
- *Implementation note (backend):* both cancel handlers currently `.Include(o => o.OrderStatusHistory)`
  only; they must add `.Include(o => o.AssignedEmployees).ThenInclude(ae => ae.Employee)` to resolve
  recipient `UserId`s. The `NotifyAsync` call lands in the same handler so the feed row + outbox push
  commit atomically with the cancellation (UnitOfWork pipeline).
- *Args:* `orderNumber` (allowlisted, lock-screen-safe — already substituted into Android/iOS bodies
  today) for the body; `orderId` in `data` for the deep-link. No new arg, no S6 change.

**Event 2 — `payroll.invoice_ready`.**
- *Trigger:* `GenerateInvoice.Handler` on success — the invoice row is created and the employee's
  unpaid `OrderEmployeePay` rows are assigned to it. Idempotent by construction: the
  `NoInvoiceExistsForPayPeriodAsync` validator guarantees **one** invoice per employee per period, so
  exactly one notification per period even under at-least-once queue redelivery.
- *Recipient:* the invoice's `Employee.UserId`. The command handler currently doesn't load the
  Employee; backend adds a `UserId` resolution (the Functions `GenerateInvoiceHandler` already loads
  `employee` and has `UserId`, but the `NotifyAsync` must sit in the command handler for the atomic
  commit). Guard null `UserId`.
- *Args:* **argless** — body is generic ("Your invoice for the latest pay period is ready to view").
  `invoiceId` rides in `data` only, for a deep-link to the partner invoices screen. This deliberately
  **avoids adding `invoiceNumber` to the D3 allowlist** (a fiscal document number on the lock screen
  would need its own S6 justification + a `TC-PUSH-APNS-5` change). Same argless shape as
  `membership.*` / `dispute.reply` today.

**Category treatment for both:** leave both keys **out** of `GetCategoryFor` → it returns `null` →
`SendPushNotificationHandler` skips the mute gate → **always delivered**. Rationale: a category is a
per-user opt-in bool **column** on `UserNotificationPreferences`; adding two new categories = a
migration + `IsAllowed`/`Set` arms + a toggle in **both** partner apps. For v1 we avoid all of that
and treat both as non-mutable operational notices (accepted-job cancellation is critical; invoice-ready
is bi-weekly and welcome). Whether they *should* be mutable is OPEN-1.

### CHALLENGER — attacks

- **CH-A (accepted, folded in): "Why not reuse the partner Android `order.cancelled` template that
  T-0393 left in place?"** Because the *key*, not the template, is the problem: `order.cancelled` is a
  customer feed key, and dispatching it to a partner `UserId` writes a row a dual-role user's customer
  app can read and binds it to the customer `OrderCancelled` toggle. The author's distinct-key design
  is correct; the dead partner template is unrelated dead code. **Stands as authored.**
- **CH-B (accepted → OPEN-3): "`invoice_ready` at *generation* announces a `Pending`, unapproved
  invoice whose PDF may not exist yet — that's not 'ready', it's 'a draft exists'."** Verified: the
  invoice is created in `Status = Pending`; PDF generation (`PdfBlobUrl`) is a separate/async step. The
  cleaner-meaningful moment is arguably `MarkInvoicePaid` ("you've been paid" — money actually moved).
  This is a genuine product choice, not an AC defect → **OPEN-3**, default = generation (the invoice
  *detail* — amounts, order count — is viewable in-app immediately even before the PDF), with
  `payroll.invoice_paid` at `MarkInvoicePaid` named as the stronger companion/alternative.
- **CH-C (accepted → OPEN-2): push vs feed-only for payroll.** No AC defect, a volume/UX choice →
  **OPEN-2**, default = push + feed.
- **CH-D (rebutted): "Non-mutable pushes violate the 'every category has a toggle' pattern."**
  `GetCategoryFor` returning `null` is an existing, supported path (the switch's `_ => null` arm; the
  handler explicitly guards `category.HasValue`). Non-mutable is a deliberate, cheaper v1 posture for
  operational notices, surfaced as OPEN-1 for the owner. Not a blocker. **Stands.**
- **CH-E (accepted, noted): dual-role raw-key exposure on iOS.** Device tokens are per-`UserId`; a
  dual-role user (same `UserId` as customer + partner) with both apps installed registers tokens for
  both under one `UserId`, so a partner-targeted dispatch multicasts to the **customer** app too. Per
  ADR-0025 D4.1 (iOS renders an absent `loc-key` verbatim), the new `push.*` keys must ship in **both**
  iOS app catalogs to avoid a raw key on a dual-role customer device. Android drops unknown keys
  silently (safe), but shipping the keys in both Android apps too is harmless and consistent. Folded
  into the sequencing constraint below.
- **Checked and found sound (silence is not assent):** the `AssignedEmployees.Any()` gate correctly
  excludes both auto-cancel paths (both act only on `Pending`, never-taken orders — verified);
  `orderNumber` is already lock-screen-visible on both platforms (no new S6 exposure); the feed-row
  write is category-independent (`INotificationProducer` doc + `NotificationProducer.cs:23`
  `IsFeedEvent` gate), so both events appear in the partner inbox purely by keyset membership; the
  argless `invoice_ready` body needs no allowlist change.

### LEAD — verdict

Consensus, zero blocking challenges. **v1 = `order.assignment_cancelled` (fully decided) +
`payroll.invoice_ready` (decided, carrying three OPEN product questions with recommended defaults —
implementable immediately on the defaults; the owner may override any one with a one-line answer
before release).** Assignment-confirmation is cut. CH-A/D rebutted; CH-B/C/E accepted and recorded as
OPENs / the sequencing constraint. The invoice-ready event is **not** cut (it has a clean, idempotent
trigger); only the choice of *which* moment and *push-vs-feed* is deferred.

### Copy (EN source — ×5 translation is a follow-up implementation step, not done here)

Partner voice, matching the existing partner pushes ("Job #A-1042 is confirmed. Tap to see the
details." / "New jobs near you: 3. Tap to view."). Format specifier is `%1$s` on Android
(`strings.xml`) and `%1$@` on iOS (`Localizable.xcstrings`).

**`order.assignment_cancelled`**
- loc-key stem: `push.order.assignment_cancelled.title` / `push.order.assignment_cancelled.body`
- Android resource names: `notification_order_assignment_cancelled_title` / `_body`
- **title (EN):** `Job cancelled`
- **body (EN):** `A job you accepted was cancelled: #%1$s. Tap for details.`
  - (`%1$s` = `orderNumber`. Deliberately does **not** name the canceller — canceller identity
    (`CancelledBy`) is not on the allowlist and not needed; neutral phrasing covers both the customer
    and admin paths.)

**`payroll.invoice_ready`**
- loc-key stem: `push.payroll.invoice_ready.title` / `push.payroll.invoice_ready.body`
- Android resource names: `notification_payroll_invoice_ready_title` / `_body`
- **title (EN):** `Invoice ready`
- **body (EN):** `Your invoice for the latest pay period is ready to view. Tap to open.` *(argless)*

### Sequencing (ADR-0025 client-first rule — hard constraint)

Per ADR-0025 D2/D5, a new event key may enter the backend APNs display map / go live from a producer
**only after** its `loc-key`s ship in both partner apps' catalogs — else iOS renders the raw
`push.*` key on the lock screen.

1. **Mobile first (both partner apps, both platforms), one release train:** add the two `loc-key`
   pairs to `Localizable.xcstrings` (partner + **customer** app per CH-E) and `strings.xml`; add the
   two keys to `PartnerFeedEventKeys.all` (Swift) and `NotificationTemplates.templateFor` +
   `formatBody` (Kotlin, partner); add deep-link arms — `order.assignment_cancelled` → order/job
   detail via `orderId` (existing partner resolver, new event_key arm); `payroll.invoice_ready` →
   partner invoices screen via the **new** `invoiceId` data key. Ship publicly.
2. **Backend second:** add the two consts to `NotificationEventCatalog`; add both to
   `NotificationFeedEventKeys.Partner`; add the two producer calls (gated as specced); add the two
   entries to the `FcmMessageFactory` D2 display map (`order.assignment_cancelled` → LocArgs
   `[orderNumber]`; `payroll.invoice_ready` → LocArgs `[]`). Do **not** activate before step 1's public
   release. (`invoice_ready` ThreadId falls back to `eventKey` since there's no `orderId`/`disputeId`
   in `data` — acceptable for a rare event; adding `invoiceId` to the factory ThreadId chain is an
   optional nicety.)

### Feed keyset membership (#4)

- `order.assignment_cancelled` and `payroll.invoice_ready` → **`NotificationFeedEventKeys.Partner`
  only.**
- **Confirmed NOT in `Customer`** — the customer feed/inbox never surfaces either event; the customer
  keyset is unchanged.
- Mirror in the partner apps: Swift `PartnerFeedEventKeys.all`, Kotlin partner `NotificationTemplates`.

### Scope cut (v1 explicitly excludes — #5)

- **Assignment-confirmation** — no notify-worthy trigger (self-service `TakeOrder`; no admin-assigns
  path). Cut.
- **Reassignment / churn events** — a cleaner being swapped/removed from an order (no such admin path
  exists today anyway). Out.
- **Shift / "your job is tomorrow" reminders** — partners have no scheduled-job reminder sweep; would
  need a new background service like the recurring reminder. Out.
- **Per-order new-job push** — availability stays the 30-min digest (`order.new_available`), not
  per-order. Unchanged.
- **`invoice_approved` / `invoice_paid` as separate events** — v1 does one payroll event; `invoice_paid`
  at `MarkInvoicePaid` is the leading follow-up candidate (see OPEN-3).
- **Mutable toggles / new `NotificationCategory` values** for the two events — v1 ships them
  non-mutable (null category); deferred (OPEN-1).
- **Customer-side promo iOS parity** — unrelated; lives in ADR-0025 Amendment A1 / T-0412 territory.

### OPEN items (recommended defaults apply if the owner is silent)

- **OPEN-1 — Should the two partner events be mutable?** Each mutable event needs a new
  `NotificationCategory` value + a `UserNotificationPreferences` bool **column** (a migration) + a
  toggle in both partner apps. **Recommended default: NO — non-mutable (null category) for v1.**
  Accepted-job cancellation is operationally critical (a cleaner must not be able to silence "your job
  was cancelled" and then show up); invoice-ready is low-frequency/high-value. Add toggles later if
  cleaners ask.
- **OPEN-2 — Is `payroll.invoice_ready` worth a push, or feed-only?** **Recommended default: push +
  feed** (bi-weekly, high-value, welcome). Alternative: feed-only, to keep partner push volume minimal.
- **OPEN-3 — `payroll.invoice_ready` trigger moment.** Generation (`GenerateInvoice` success — invoice
  viewable immediately, PDF may lag) **[recommended default]** vs approved (`ApproveInvoice`) vs paid
  (`MarkInvoicePaid` — money moved; arguably the most valuable to a cleaner). If the product intent is
  "you've been paid," rename to `payroll.invoice_paid` and hook `MarkInvoicePaid` instead. Also depends
  on a partner invoices screen existing for the deep-link target; if none exists, the tap opens the app
  and the feed row is informational.

## Status log
- 2026-07-18 — **`order.assignment_cancelled` SHIPPED** on `feature/i18n-cluster-3` (backend + all
  four mobile catalogs, client-first per ADR-0025). Backend: catalog const (non-mutable via the
  `GetCategoryFor` null default — a cancellation must not be silenceable), partner feed keyset,
  `FcmMessageFactory` map (`[orderNumber]`), and a shared `OrderAssignmentCancellationNotifier`
  wired into BOTH `CancelOrder` and `AdminCancelOrder` (gated on `AssignedEmployees.Any()` with the
  `.Include(AssignedEmployees).ThenInclude(Employee)`; skips legacy null-user rows) — auto-cancel
  paths excluded by the gate (never have an assigned crew). Mobile: `push.order.assignment_cancelled.*`
  ×5 in all 4 catalogs (partner + customer, both platforms — a dual-role cleaner receives the push
  on either app; the customer feed keyset is UNCHANGED so it stays partner-inbox-only, but the push
  renders instead of showing a raw loc-key), partner render + deep-link (→ order detail) both apps.
  Tests: `OrderAssignmentCancellationNotifierTests` (notify-each / no-op-empty / skip-null-user),
  the feed-keyset + FcmMessageFactory pins bumped to 13 events, iOS partner render+tap pins; backend
  suite 1979/1979, both Android apps + partner iOS build green.
- **`payroll.invoice_ready` DEFERRED to the owner** — it carries genuine product forks the analyst
  flagged (OPEN-2 push vs feed-only; OPEN-3 trigger moment: generation vs approved vs **paid** — the
  "you've been paid" semantics differ and would rename the event `payroll.invoice_paid`), and it
  needs a partner *invoices* deep-link target that does not yet exist on mobile. Not guessed; awaits
  the owner's call, then a fast follow-up.
