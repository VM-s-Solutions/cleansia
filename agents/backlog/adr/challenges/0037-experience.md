# ADR-0037 — Challenge (challenger mode, lane: what partners and customers actually experience)

Gate 0 discipline: every claim below carries `file:line` read in this session. Where the ADR is right,
I say so in the last section rather than staying silent.

Headline: **the ruling is sound; the experience layer it lands on is not, and the ADR's own copy plan
and reviewer checklist would not detect that.** Nine findings, ranked by how many cleaners hit them per
day.

---

### CH-X1 — The ADR cannot deliver a refusal message on iOS as specified, because the partner app reads the *customer's* string table. Today a cleaner who loses a race is told **"No cleaners are available for that slot. Please pick another time."**

The brief asked me to write the actual message. I did not have to invent one — it already exists and it
is a customer's sentence shown to a cleaner.

Chain, every hop read:

1. iOS partner take → `OrdersListViewModel.swift:190` `client.takeOrder`, refusal handled at `:178-184`
   → `snackbar.showApiError(error)` (`:182`). Detail screen: `OrderDetailViewModel.swift:93,155`.
2. `SnackbarController.swift:34-37` → `localizer.message(for: error)`.
3. `ApiErrorLocalizer.swift:13-33` resolves `"error." + key` **only** in `CoreL10n.bundle` (`:31`) —
   the `CleansiaCore` module bundle. It never consults the app bundle.
4. `CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings:3502-3532` —
   `error.order.no_available_spots`, `en`: **"No cleaners are available for that slot. Please pick
   another time."** (`:3514`)
5. That string was authored for the **customer booking flow** and says so:
   `CleansiaCustomer/Sources/Features/Booking/BookingSubmitOutcome.swift:9` names
   `error.order.no_available_spots` as one of "the server's already-translated business message[s]" the
   *booking sheet* surfaces.
6. `CleansiaPartner/Resources/Localizable.xcstrings` contains **no** `error.order.*` key — I grepped it;
   zero matches. There is no partner override and no mechanism for one.

> **Given** a cleaner on iOS Partner viewing the Available tab
> **When** they tap "Take Order" on a job another cleaner took two seconds earlier
> **Then** the snackbar reads *"No cleaners are available for that slot. Please pick another time."*

That is both untrue (they are the cleaner) and unactionable (there is no time for them to pick). The
ADR's D6 bill says iOS costs "1 file, 5 languages" in exactly this shared table — so the new
`order.not_takeable` copy inherits the same defect class: a partner-only sentence written into a table
the customer app also resolves from, with nothing preventing the next partner key from being read on a
customer surface or vice versa. **The ADR treats iOS localisation as one file with five rows. It is one
file with two personas and no separator.**

For contrast, the same key on the other two clients is fine:
`partner-app/src/main/res/values/strings.xml:1098` = "No spots available on this order.";
partner web `en.json:1075` = "No available spots for this order."

**What I need from the author:** either (a) the ADR states that partner-facing `error.order.*` copy must
live in `CleansiaPartner/Resources/Localizable.xcstrings` and `ApiErrorLocalizer` must probe the app
bundle before `CoreL10n.bundle`, or (b) it accepts persona-neutral copy as a hard constraint on every
shared key and fixes `no_available_spots` in the same change. Silently adding a 16th string to the
shared table is not an option — it is how this defect was created.

---

### CH-X2 — The seat ruling made the *other* key the common one. The ADR budgets copy for `order.not_takeable` (rare) and none for `order.no_available_spots` (now the modal refusal), which it explicitly declined to touch.

The owner's ruling is implemented: `BookingPolicy.cs:76` `SpareSeatsPerOrder = 0`;
`Order.cs:514-522` `MaxEmployees = RequiredEmployees + spareSeats`; `OrderFactory.cs:148` passes it.
`Cleansia.Tests/Features/Orders/OrderSeatCapacityTests.cs:37-39` asserts
`RequiredEmployees == MaxEmployees == AvailableSpots`. The modal booking
(`EstimatedTime ≤ 120` ⇒ `RequiredEmployees = 1`) now carries **exactly one seat**.

Now trace the ordering the ADR itself mandates (D6): `ExistsAsync → IsOfferable → HasAvailableSpots`.
A job that was just taken is `Confirmed` (`TakeOrder.cs:192-194`) — so `IsOfferable` **passes** and the
refusal falls through to `HasAvailableSpots` → `BusinessErrorMessage.NoAvailableSpots`
(`TakeOrder.cs:44-45`). **The race never produces `order.not_takeable`.** It produces the key D6's
rejection table dismisses as *"Lies. The seat exists; the order is dead."* — which is itself now wrong:
after the seat ruling the seat does **not** exist, and the message is the only one most cleaners will
ever see.

Frequency is not incidental, it is manufactured by the push:

- `NewJobsDigestService.cs:62-74` selects **every** Approved/Active cleaner with a `WorkCountryId` —
  no radius, no cap, no shortlist.
- The per-cleaner query (`:98-114`) filters on country + free seat + not-already-mine + status. Nothing
  else. Every cleaner in the country is told about the same job.
- `partner-app/.../strings.xml:1209-1210` — `notification_new_jobs_title` "New jobs available",
  `notification_new_jobs_body` "%1$d new jobs available near you. Tap to view."
- No reservation, no hold, no staggering (ADR-0036's hold is a *preferred-cleaner* mechanism, not a
  race breaker for the open board).

> **Given** a single-seat cash job in CZ and 20 approved cleaners in CZ
> **When** the digest fires and all 20 receive "1 new job available near you. Tap to view."
> **Then** one succeeds and **19** are told "No spots available on this order." — a message that names
> a capacity concept rather than the thing that happened ("someone got there first")

Before the seat ruling `MaxEmployees = 2` on that job, so the same fanout produced ~18 refusals instead
of 19 — the change is small in the tail but it removes the *only* slack the modal job had. The real
issue is that the ADR's copy work is aimed at the wrong key: **`order.not_takeable` fires when the job
died (cancelled/completed/unpaid-card); `order.no_available_spots` fires when a colleague was faster,
and that is now the default outcome of every push.**

**Ask:** D6's "Why a new key and not an existing one" table must gain a row for the *inverse* question —
given `IsOfferable` runs first, what does the loser of the common race see, and is `no_available_spots`
still the right sentence at seat-cap 1? My reading: it needs rewording to name the race
("Another cleaner has already taken this job.") across all 11 files, which roughly doubles the ADR's
stated string bill. That is a scope fact the PM needs before ticketing, not after.

---

### CH-X3 — On partner **web**, a refused take does not clear the row. The cleaner can tap the same dead job forever. The ADR ships a brand-new refusal into the one client that cannot recover from it.

Both web take call sites drop the error on the floor:

`orders.facade.ts:207-218` (the Available board):
```ts
this.partnerClient.orderClient
  .takeOrder(new TakeOrderCommand({ orderId }))
  .subscribe((response) => {
    if (response) {
      this.snackbarService.showSuccessTranslated('pages.orders.order_taken_success');
      this.loadAvailableOrders();
      this.loadMyOrders();
    }
  });
```
No error callback. The reload is **inside** the success branch. (It is also the only call in that facade
without `takeUntil(this.destroyed$)` — compare `:222`, `:120`.)

`order-details.facade.ts:189-202` (the detail page): `catchError(() => of(null))` — swallowed, and
`loadOrderDetails` is inside the `tap`, so the detail page never re-reads either.

The refusal *is* announced — `http-error.interceptor.ts:26-49` shows a snackbar — but nothing tells the
board the row is stale.

Both mobile clients do the right thing and prove this is a web-only gap:
- Android `OrdersListViewModel.kt:355-368`: on error → snackbar → `invalidatePanesFor(mutation)` →
  `fetchAsync(..., background = true, notifyOnError = false)`. The comment at `:359-365` names exactly
  this scenario: *"A reject nearly always means the order moved on without us — another cleaner took it
  … so the row must stop offering an action the server has already refused."*
- iOS `OrdersListViewModel.swift:178-184`: identical shape, comment "O4: clean reject (e.g.
  already-taken) — spring the slider back, surface, and refresh the current pane so the stale row
  corrects."

And there is no confirmation step to absorb the mistake: `orders.component.ts:235-237` →
`facade.takeOrder(order.id!)` fires straight off `orders.models.ts:168`'s `onClick`.

> **Given** a cleaner on partner web with the Available board open
> **When** they click the green check on a job that was taken/cancelled since render
> **Then** a red toast appears for 3s (`snackbar.service.ts:207` `DEFAULT_SNACKBAR_DURATION`), **the row
> stays**, its green check stays enabled, and clicking it again produces the identical toast — with no
> state change to distinguish "I already tried this" from "I haven't tried yet"

The ADR's D6 justification is "the gate is ~6 lines against a validator being opened anyway." True for
the backend. On web the honest bill includes an error branch on two facades, or the ADR is knowingly
adding a refusal path to a UI with no reconcile. **This blocks unless the ADR either lists the web
reconcile as in-scope for T-0530 or files it explicitly with a ticket title.**

---

### CH-X4 — Reviewer step 6 is **false for the web partner**: a missing translation shows the *generic* message, not the raw key. And the ADR's own cost exemplar, `order.weekly_limit_reached`, is missing from partner web today — so the failure mode is already live and has never been caught.

ADR "How a reviewer verifies compliance" step 6: *"`order.not_takeable` resolves in all 11 locale files;
a missing one shows the raw key."*

That is true on Android — `ApiErrorTranslator.kt:60-79`, `lookupKey(key) ?: key` (`:70`, `:76`), and the
doc comment at `:55-58` says so deliberately. It is true on iOS —
`ApiErrorLocalizer.swift:18-20` returns the bare `key` on a catalog miss.

It is **false on web**, by design:
```ts
// http-error.interceptor.ts:14-20
const candidateKey = `api.${String(errorKey)}`;
const message = translate.instant(candidateKey);
// ngx-translate echoes the key back when it has no translation — never let a
// raw machine key reach the snackbar; fall back to the generic message.
return message === candidateKey ? translate.instant(GENERIC_ERROR_KEY) : message;
```
A missing `api.order.not_takeable` renders `api.common.error_occurred` = **"An error occurred. Please
try again."** (`cleansia-partner.app/.../en.json:1021`). Indistinguishable from a 500. A reviewer
following step 6 literally — look for the raw key on screen — will conclude the key resolved.

This is not hypothetical. I grepped `weekly_limit_reached` repo-wide:

| Client | Present? |
|---|---|
| Android partner | yes — `values/strings.xml:1099` + cs/sk/uk/ru |
| iOS shared | yes — `Localizable.xcstrings:4062` |
| Customer web | yes — `cleansia.app/.../en.json:1467` ×5 |
| **Partner web** | **NO — zero matches in any of the five `cleansia-partner.app` locale files** |

`BusinessErrorMessage.cs:73` `WeeklyOrderLimitReached = "order.weekly_limit_reached"` is thrown by
`TakeOrder.cs:57-58` — the very validator this ADR is editing. So **today**, a cleaner on partner web
who hits their 3/6/10 weekly cap is told "An error occurred. Please try again." and retries forever.

And nothing catches it: `error-contract-parity.spec.ts` is customer-scoped —
`:27-30` pins `I18N_DIR` to `Cleansia.App/apps/cleansia.app/src/assets/i18n`. The ADR states this at D6
and then relies on a manual step 6 that the web's own fallback defeats.

**Ask:** (a) reword step 6 to "grep the five partner-web locale files for the literal key" rather than
"watch the screen"; (b) the ADR should note that `order.weekly_limit_reached` is missing from partner
web as an existing instance of the same gap — it is the strongest available argument for the parity
test the ADR wants, and it is sitting in the file the ADR cites as its cost model.

---

### CH-X5 — The census is eight surfaces; there are **ten**. The two missed ones are the web *buttons*, and one of them contradicts the ruling in the direction that hides work: the web order-detail "Take Order" button is gated `{Pending, Confirmed}` — it **excludes `New`**, i.e. exactly the cash case the ADR calls its strongest argument.

**Surface 9 — the Available row action.** `orders.models.ts:169-176`:
```ts
visible: (row: OrderListItem) => {
  const status = row.orderStatus?.value;
  const isTakeable =
    status === OrderStatus.New ||
    status === OrderStatus.Pending ||
    status === OrderStatus.Confirmed;
  return isTakeable && (row.availableSpots ?? 0) > 0;
}
```
A fourth web status literal, carrying the dead `Pending`. D4 row 5 only names `orders.facade.ts:142-146`.

**Surface 10 — the detail-page Take button.** `order-details.helpers.ts:108-115`:
```ts
export function canTakeOrder(orderStatusValue, assignedEmployees, employeeId): boolean {
  const isPendingOrConfirmed =
    orderStatusValue === OrderStatus.Pending || orderStatusValue === OrderStatus.Confirmed;
  return isPendingOrConfirmed && !isEmployeeAssigned(assignedEmployees, employeeId);
}
```
Consumed at `order-details.component.ts:139-144`. Since `Pending` has no writer (the ADR's own Fact 1),
this is `{Confirmed}`. So:

> **Given** a cash order sitting at `New` — the case Fact 2 calls "the strongest single argument that
> `New` must be offerable"
> **When** a cleaner on partner web opens it from the board instead of clicking the inline check
> **Then** the "Take Order" button is **absent**. The job is listed, is takeable by the server, and the
> detail page offers no way to take it.

The ADR's ruling makes this a direct contradiction: `IsOfferable(New, Cash) = true`, but the only
per-order screen on web says no. Both mobile clients get it right and prove it is a web bug, not a
design choice — iOS `OrderPrimaryAction.swift:44-48` maps `._0` (New) → `.take` for a non-assignee and
`._1` (Pending) → `.none` at `:65-67`; Android `OrderPrimaryAction.kt:57-58` gates on
`OrderStatus._0, OrderStatus._2`.

**And D7's enforcement layer would not see it.** The proposed `available-status-parity.spec.ts` asserts
"the Available-tab status literals in `orders.facade.ts`, `OrdersListViewModel.kt` and
`OrdersListLogic.swift`". Three files. Surfaces 9 and 10 are in `orders.models.ts` and
`order-details.helpers.ts` — the spec goes **green** while the button that actually gates the click
stays wrong. A parity test that covers the query and not the button tests the wrong half: the query
determines what is *listed*, the button determines what is *clickable*, and the ADR's whole thesis is
that those two must not diverge.

**Ask:** D0's table gains rows 9 and 10; D4 gives them verdicts; D7's parity spec enumerates the
button gates, not just the query literals.

---

### CH-X6 — The web status filter makes cash jobs unreachable and offers a guaranteed-empty option. This is a *cliff*, not a gap: touching the filter at all deletes `New` from the query.

`orders.helpers.ts:46-57` `buildOrderStatusOptions` returns:
`{Pending, Confirmed, OnTheWay, InProgress, Completed, Cancelled}` — **`New` is not an option**, and
`Pending` (dead) is.

Now combine with `orders.facade.ts:142-146`:
```ts
orderStatuses: additionalFilters?.orderStatuses || [OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed],
```
The default is only used when the cleaner has set **no** status filter. The moment they pick anything,
their selection replaces the whole list.

> **Given** a partner-web board whose available work is mostly `New` cash jobs (Fact 2's pipeline)
> **When** the cleaner uses the status filter at all — even selecting "Confirmed" to narrow down
> **Then** every `New` cash job disappears from the board, and there is no option in the dropdown that
> brings them back short of clearing the filter entirely
>
> **And when** they pick "Pending" (an option the UI offers)
> **Then** the board is empty, always, because nothing writes that status

After this ADR the first branch gets worse, not better: `New` + Cash becomes the canonical pre-take
state for the entire cash pipeline, and it is the one state the filter cannot express. The ADR removes
`Pending` from `orders.facade.ts` (D4 row 5) but says nothing about the dropdown that still offers it.

**Ask:** in scope or filed with a title — `buildOrderStatusOptions` drops `Pending`, adds `New`.
It is three lines in a file already being edited for the same reason.

---

### CH-X7 — The count-vs-list contradiction is **moved, not fixed**. The ADR aligns the status/payment term and leaves the **date floor** unaligned — and §D9.2(a) *creates* that divergence on mobile, where today there is none.

The status half of the ADR's claim is confirmed. `DashboardSpecifications.cs:24` is
`{Pending, Confirmed}` ≡ `{Confirmed}`, consumed by both the count (`GetDashboardStats.cs:236`) and the
preview (`GetAvailableJobsPreview.cs:50`). Android's dashboard hero reads
`preview.totalAvailableCount` (`DashboardScreen.kt:510, 547` → `dash_available_now_count` = "%1$d jobs
available", `strings.xml:105`) and falls to `dash_no_jobs_yet_title/subtitle` at `DashboardScreen.kt:613`
when it is 0. So yes — today "0 jobs available / Check available work" sits above a list of cash jobs.
The ADR fixes that.

But the two queries have a **second** difference the ADR does not touch:

| | date floor |
|---|---|
| Dashboard count + preview (`DashboardSpecifications.CreateAvailableOrdersSpec`) | **none** — `cleaningDateFrom: null` (`:18`) |
| Available list (`GetPagedOrders.cs:57-61`) | `now − 2h`, applied **only when `HasAvailableSpots == true`** |
| Available list, web | `now` — `orders.facade.ts:149` `cleaningDateFrom ?? new Date()` (stricter still) |

The `-2h` default lives in the `GetPagedOrders` handler, not in the specification, so the dashboard
spec cannot inherit it.

And D9.2(a) celebrates the mobile switch to `hasAvailableSpots: true` as "one change, two fixes —
the floor fires." It does fire, on the **list**. The dashboard hero above it still has no floor.

> **Given** an Android cleaner, and 5 offerable single-seat jobs of which 2 are scheduled for yesterday
> **When** they open the dashboard after this ADR ships
> **Then** the hero reads **"5 jobs available"** with "Tap to browse and take one"
> (`strings.xml:105-106`); tapping through shows **3** rows and the header reads "3 jobs · earn up to …"
> (`OrdersListScreen.kt:398,438` — `filtered.size`)

Today those two numbers agree on the date axis, because neither has a floor. **After the ADR they
disagree.** The ADR's stated goal is "a partner can see '0 available jobs' beside a list of jobs";
shipping it as drafted converts that into "a partner sees N available jobs beside a list of N−k jobs" —
a smaller wrong, still a wrong, and newly introduced by this change on two clients.

**Ask:** either move the `-2h` floor into `DashboardSpecifications.CreateAvailableOrdersSpec` (one line,
same file being edited by D4 row 2) or the ADR states explicitly that the dashboard count is
deliberately floor-free and why a cleaner should read a count that includes work they cannot see.
Also note the residual web asymmetry: web's list floor is `now`, the server's is `now − 2h`, so the web
count/list will still differ by any job in that two-hour band.

---

### CH-X8 — The abandonment window the ruling leans on: the sweep cancels **silently** (the only cancel path in the system with no customer notification), and a customer who pays late gets `Cancelled → Confirmed` — which falsifies D1's "`Cancelled`: Terminal."

The ADR's D0 refutation is correct and I verified it independently: `CleanupStalePendingOrders.cs:51-53`
keys on `PaymentStatus.Pending && PaymentType == Card && CreatedOn < cutoff` with **no `OrderStatus`
term**, and `CleanupStalePendingOrdersHandler.cs:21` sends `OlderThanHours: 1` on a 15-minute timer
(`:8-12`). The ~1h15m window is right.

Two things inside that window the ADR does not model.

**(a) The customer is never told.** `CleanupStalePendingOrders.Handler:69-79` writes
`UpdatePaymentStatus(Failed)` + `AddOrderStatus(Cancelled)` and **dispatches nothing**. No push, no
email. Every sibling cancel path does: `HandlePaymentNotification.cs:306-319` (Stripe expiry) and
`AutoCancelStaleRecurringOrders.cs:86-90` both emit `NotificationEventCatalog.OrderCancelled`. I grepped
that constant across `src/` — the sweep is the one production writer of `OrderStatus.Cancelled` that
does not.

> **Given** a customer who opened Stripe checkout at 14:00, got distracted, and closed the tab
> **When** the sweep runs at 15:07
> **Then** their booking silently becomes Cancelled. Their next visit to the orders list shows a
> cancelled booking they were never told about and cannot explain — and no cleaner was ever offered it,
> so there is no cleaner-side trace either

**(b) `Cancelled` is not terminal.** `HandleCompletedSession` short-circuits only on
`PaymentStatus is Paid or Refunded` (`HandlePaymentNotification.cs:254`). The sweep leaves
`PaymentStatus.Failed`, which is **not** in that set. So:

> **Given** the same order, cancelled by the sweep at 15:07
> **When** the customer returns at 15:30 and completes the still-live Stripe Checkout session
> **Then** `HandlePaymentNotification.cs:260-261` writes `PaymentStatus.Paid` + an `OrderStatus.Confirmed`
> track, enqueues the receipt (`:267-273`) and pushes `OrderConfirmed` (`:277-287`).
> The order's history reads `New → Cancelled → Confirmed`, and it re-enters the offerable set — now with
> less lead time than the customer booked with

That outcome is *arguably right* for the customer (their money was taken; they should get the clean).
But D1's table says `Completed, Cancelled → NO — Terminal. Closes Fact 3.` and D6 leans on that word to
justify a hard refusal. **`Cancelled` has a live un-cancel path, and the ADR's model does not contain
it.** A cleaner who saw a job, watched it vanish, and sees it reappear an hour later has no explanation
available on any screen.

This also punches a hole in reviewer step 8. Its fixture is *"a `New` **card** order (not offered, not
counted, not takeable)"* — a single snapshot. It does not exercise the sweep, the late payment, or the
re-entry. The one behaviour that makes the card ruling defensible ("an unsettled card order is cancelled
out from under the cleaner within ~1h15m by a sweep that is already running", the ADR's own opening
summary) is asserted and never tested.

**Ask:** (1) D1's `Cancelled` row is qualified — terminal *for the take gate*, not terminal in the
lifecycle, with `HandlePaymentNotification.cs:254` cited; (2) the silent-cancel notification gap gets a
filed ticket title (it is a customer-facing hole the ADR's central premise depends on); (3) AC4's
fixture gains the late-payment resurrection as a case, or the ADR states it is out of scope in writing.

---

### CH-X9 — The two "same disease, filed separately" rows are on the same screen the refused cleaner lands on, and one of them is worse than filed: the status timeline is off by one, so a **Cancelled** order renders with the **OnTheWay** icon.

The ADR's D8 row 3 is confirmed exactly as written: `dashboard.facade.ts:93-97` sends
`{Pending, Confirmed, InProgress}` for web "my upcoming" — dead `Pending`, no `OnTheWay`. A job
disappears from the cleaner's own dashboard the moment they tap "on my way", i.e. at the moment they
most need it. Filing it is defensible.

What is not in the ADR at all is `order-details.helpers.ts:69-88`:
```ts
const STATUS_CLASS_MAP: Record<number, string> = {
  1: 'status-pending', 2: 'status-confirmed', 3: 'status-inprogress',
  4: 'status-completed', 5: 'status-cancelled',
  // OnTheWay = 6 — between Confirmed and InProgress in workflow but appended
  // numerically. See backend OrderStatus.cs for why the value isn't slotted
  // between 2 and 3.
  6: 'status-ontheway',
};
```
The comment is factually wrong. `Cleansia.Core.Domain/Enums/OrderStatus.cs:8-14` and the generated
`partner-client.ts:10375-10383` both say `New=0, Pending=1, Confirmed=2, OnTheWay=3, InProgress=4,
Completed=5, Cancelled=6`. The map is shifted: `3` (OnTheWay) paints `status-inprogress`, `4`
(InProgress) paints `status-completed`, `5` (Completed) paints `status-cancelled`, `6` (**Cancelled**)
paints `status-ontheway` with `pi pi-send` (`:81-88`), and `New=0` falls to the `?? 'status-pending'`
default.

Why it belongs in this deliberation rather than a random backlog: the ADR is introducing a refusal whose
only self-service explanation is "open the order and look at its history." That history is exactly this
timeline. **A cleaner refused with `order.not_takeable`, who opens the order to find out why, sees a
Cancelled order wearing the "on my way" badge.** It is a second false mirror of the same species D7 is
built to kill ("a comment asserting agreement between two things"), and it sits in a file the ADR is
already opening for CH-X5's `canTakeOrder`.

**Ask:** file with a title, or fix in-flight. Either way name it — it is currently invisible to the
panel.

---

## What I checked and found sound

Named explicitly, per protocol — silence is not assent.

- **The seat-cap ruling is implemented, correctly and in one place.** `BookingPolicy.cs:76`
  (`SpareSeatsPerOrder = 0`) → `OrderFactory.cs:148` → `Order.cs:514-522`. I ran the ADR's own reviewer
  step 12 mentally: the `+ spareSeats` arithmetic appears only in `Order.CalculateRequiredEmployees`;
  every other hit (`OrderSpecification.cs:126,138`, `Order.cs:116`, `NewJobsDigestService.cs:101`,
  `OrderMappers.cs:101`, `OrderAccessService.cs:85`) reads the `MaxEmployees` property. D9.4's
  property-not-formula rule holds. `SetMaxEmployees` (`Order.cs:526-533`) is still test-only.
- **D9.2(b) is real and the author's reading is right.** `OrderSpecification.cs:134-139`
  `RestrictToEmployeeId` is `assigned-to-me OR has-a-free-seat` — it does **not** exclude the caller's
  own rows, and `ExcludeEmployeeId` (`:129-132`) is a separate, opposite-polarity clause. Web already
  compensates (`orders.facade.ts:148`). Shipping `hasAvailableSpots` without `excludeEmployeeId` on
  mobile would indeed list jobs the cleaner is on, each erroring on tap via `TakeOrder.cs:55`. The
  "hard reject a diff that changes one without the other" instruction is warranted.
- **D9.2(a): the `-2h` floor does fire.** `GetPagedOrders.cs:57-61` gates on
  `Filter.HasAvailableSpots == true && cleaningDateFrom is null`; neither mobile client sends
  `cleaningDateFrom` on the Available tab (`OrdersListViewModel.kt:264-267` passes `null to null` for
  non-History tabs; `OrdersListLogic.swift:77-85` passes `cleaningDateFrom: nil`). So the switch closes
  the past-dated-jobs defect on the *list* as claimed. (My CH-X7 is about the count, not the list.)
- **Fact 1 (`OrderStatus.Pending` has no writer) — independently confirmed** on the paths I touched:
  `OrderFactory.cs:166` New, `TakeOrder.cs:194` Confirmed, `HandlePaymentNotification.cs:261` Confirmed
  / `:304` Cancelled, `CleanupStalePendingOrders.cs:77` Cancelled. Both mobile clients already treat it
  as dead (`OrderPrimaryAction.swift:65-67`, `OrderPrimaryAction.kt:57`).
- **Fact 2 (a cash order stays `New`) — confirmed at the take.** `TakeOrder.cs:190-196`: the `Confirmed`
  track is written only when `currentStatus is New or Pending`, i.e. the take **is** the confirmation
  for cash, and the customer's `OrderConfirmed` push + email fire from that same branch (`:198-225`).
  So on cash, the cleaner's tap is what confirms the customer's booking. I looked specifically for a
  partner surface written on the assumption "takeable ⇒ already confirmed" — the mobile clients are
  clean, and the one that isn't is `canTakeOrder` (CH-X5).
- **`IsOfferable` before `HasAvailableSpots` is the right order** for the case D6 argues (a `Cancelled`
  order with a free seat), and I could not construct a case where it produces a worse message than the
  reverse. My objection in CH-X2 is about which key carries the *common* case, not about the ordering.
- **Both mobile clients reconcile a rejected action correctly** — Android
  `OrdersListViewModel.kt:355-368`, iOS `OrdersListViewModel.swift:178-184`. A refused take on mobile
  clears the row without a visible spinner and without a duplicate toast. This is the behaviour web
  lacks (CH-X3), and it is well built.
- **`error-contract-parity.spec.ts` is customer-scoped as the ADR states** — `:27-30` pins the i18n dir
  to `cleansia.app`. D6's warning that it "will not catch a miss" for partner keys is accurate.
- **`OrderStatus.Pending` must stay on the wire.** Confirmed it is emitted as an int in all four
  generated clients I checked (`partner-client.ts:10377`, `customer-client.ts:10947`,
  `admin-services/admin-client.ts:23163`) — A11's "deprecate, don't delete" is right. Noted in passing:
  `libs/core/services/src/lib/client/admin-client.ts:7180-7186` carries a *stale* `OrderStatus` with
  entirely different integers (`Pending=1, Confirmed=2, InProgress=3, Completed=4, Cancelled=5`, no
  `New`/`OnTheWay`). Not this ADR's problem, but any future reader grepping "the generated enum" can
  land on the wrong one.
- **What I checked and did not find a problem in:** `SlotBlockingStatuses` reasoning (I did not re-open
  it — D8 marks it inspected and out of scope, and nothing in my lane depends on it); the pay formula
  (untouched, as D9.5 claims); the tenancy filter; `MarkCashCollected` gating
  (`order-details.helpers.ts:197-208`, correctly `InProgress` + not-Paid + assigned, and its
  payment-type comment at `:194-196` is accurate).

---

**Blocking, in my judgement:** CH-X1, CH-X3, CH-X5, CH-X7. Each changes what a cleaner sees or clicks
and none is answerable by "the implementer will notice."
**Non-blocking but must be answered in writing:** CH-X2, CH-X4, CH-X8.
**File-or-fix, author's call:** CH-X6, CH-X9.
