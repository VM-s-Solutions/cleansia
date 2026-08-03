# ADR-0037 — Which orders a cleaner may be **offered** and which they may **take** is **one rule, evaluated at two moments**: an order is offerable iff its **fulfilment** axis is pre-work-with-a-free-seat **and** its **money** axis has already reached the state the take assumes — concretely `Confirmed`, plus `New` **only for `PaymentType.Cash`**; the rule lives once in `Cleansia.Core.Domain.Orders.OrderAvailability`; `TakeOrder` **gains a status gate** (new key `order.not_takeable`, 15 strings in 11 files); and `OrderStatus.Pending` is **declared dead** — the documented "card payment initiated" lifecycle is already implemented on the **payment** axis, so the missing writer is not missing, it is a duplicate that was never built

- **Status:** `proposed` — drafted 2026-08-02 by the architect (author mode) for **T-0530 AC1**. Not
  yet challenged. **Not binding until a lead declares consensus** per `agents/process/deliberation.md`.
- ⚠️ **AMENDED BY OWNER INSTRUCTION, 2026-08-03 — `Q-AVAIL-01` IS ANSWERED: a second cleaner MAY join a
  partly-staffed job.** The ruling and its consequences are **§D9**, added below; the dated amendment
  note is at the end of this file. **The status axis this ADR rules is untouched** — D9 is the *seat*
  axis, a second conjunct, exactly as D8 said it would be. Nothing above D9 has been rewritten: D8's
  out-of-scope row and the `Q-AVAIL-01` escalation are marked **ANSWERED** in place with a pointer, so
  the original framing of the question is still readable.
- **Date:** 2026-08-02 (drafted) / **2026-08-03 (amended — owner ruling on `Q-AVAIL-01`, §D9)**
- **Supersedes:** — (composes with **ADR-0036** — the preferred-cleaner hold, whose `## Open` list
  item *"should the available-orders board include `New` orders?"* this ADR **closes**; **ADR-0011** —
  the mobile result contract the new error key rides; **ADR-0031** — NSwag regen drift, **not**
  triggered here)
- **Superseded by:** —
- **Applies to:** `Cleansia.Core.Domain` (one new policy class, no schema change) ·
  `Cleansia.Core.AppServices` (the digest, the dashboard spec, the order specification, `TakeOrder`,
  one new `BusinessErrorMessage` constant) · `Cleansia.App` partner web · `cleansia_android`
  partner-app · `cleansia_ios` CleansiaPartner · **no migration** · **no NSwag regen** (no DTO or
  endpoint shape changes; the new key rides the existing ProblemDetails channel) · **no host
  coupling** · **no change to the tenancy filter, the pay formula, or the fiscal modes**
  · **(added 2026-08-03, §D9)** the two mobile partner clients additionally change **which query
  parameters they send** — `isUnassigned` → `hasAvailableSpots` **+ `excludeEmployeeId`**. Still **no
  backend change and no NSwag regen** for that: both parameters are already on the endpoint
  (`cleansia_android/openapi/partner-mobile-api.json:1128,1142`).
- **Ticket:** T-0530 (AC1 is this document; AC2/AC3/AC4 are implemented against it).
  Serialization: T-0529 → **T-0530** → T-0528 all edit `NewJobsDigestService.cs`; **T-0515**
  (ADR-0036) edits four of the same surfaces and must land after this rule exists, not beside it.

> **One decision:** *what makes an order offerable to a cleaner.* Everything else here is a corollary.
> The answer is not a status list. It is: **an order is offerable when nothing that is still in flight
> can retract it.** For cash nothing is in flight — the take *is* the confirmation. For card the money
> is in flight until it settles, and an unsettled card order is cancelled out from under the cleaner
> within ~1h15m by a sweep that is already running.

---

## Why this is ADR-weight and not a one-line ruling

T-0530 scoped this as *"one architect, one item"* — reasonable when the ticket believed the divergence
was three-way and cosmetic. Verification found otherwise, and four of the findings are decision-shaped:

1. **The divergence is eight-way, not three-way** (§D0), and two of the eight are *server-side
   authorization* surfaces, not display.
2. **The rule is not expressible as a status set at all** — it is payment-qualified, which no existing
   surface implements and which changes what "canonical set" even means.
3. **It changes shipped behaviour** (the take gate) and costs a new error key in three clients.
4. **It declares an enum member dead against the documented lifecycle in `CLAUDE.md`** — the
   canonical project guide is wrong, and correcting it needs a citable record.

A living decision doc cannot carry (3) or (4): they must be immutable and citable. So: ADR-0037, with
`agents/architecture/decisions/order-availability.md` as the living companion.

---

## D0 — The evidence (Gate 0: every row read, not inherited)

The brief supplied six surfaces and three facts. All six surfaces are confirmed. **Two further
surfaces were found**, one of them the most consequential in the set. **Two of the three supplied
facts are confirmed as stated; the third is confirmed in its conclusion but its stated mechanism is
refuted** — and the refutation strengthens the ruling.

### The eight surfaces that answer "which orders may a cleaner work on"

| # | Surface | Today | Kind |
|---|---|---|---|
| 1 | `NewJobsDigestService.cs:52-53` | `{New, Pending, Confirmed}` | push |
| 2 | `DashboardSpecifications.cs:24` | `{Pending, Confirmed}` | count + preview |
| 3 | `GetPagedOrders.cs:87` | client-supplied, **no server floor** | list |
| 4 | **`OrderSpecification.cs:134-139` `RestrictToEmployeeId`** | **status-blind seat arithmetic** | **server-side visibility** |
| 5 | `orders.facade.ts:142-146` (web) | `{New, Pending, Confirmed}` | client display |
| 6 | `OrdersListViewModel.kt:248` (Android) | `{_0, _2}` = `{New, Confirmed}` | client display |
| 7 | `OrdersListLogic.swift:78` (iOS) | `{._0, ._2}` = `{New, Confirmed}` | client display |
| 8 | **`TakeOrder.Validator` `:38-60`** | **no status rule** | **write gate** |

**#4 is the surface nobody counted, and it is the one that matters most.** For a non-admin caller
`GetPagedOrders.cs:91` pins `restrictToEmployeeId`, and `OrderSpecification.cs:136-138` expands it to:

```csharp
x.AssignedEmployees.Any(ae => ae.EmployeeId == RestrictToEmployeeId)
|| x.AssignedEmployees.Count < x.MaxEmployees
```

The second disjunct is **pure seat arithmetic with no status term**. This is the server's *only*
authoritative floor on what a browsing cleaner may read, and it admits every `Cancelled` and
`Completed` order that has a free seat — which, per `Order.cs:519` (`MaxEmployees = RequiredEmployees
+ 1`), is nearly all of them. Surface #3's missing floor is therefore not a separate defect: **#3 has
no floor because #4 is the floor, and #4 is status-blind.** One fix closes both.

Two further surfaces are adjacent and **explicitly out of scope** (§D8): `OrderRepository.cs:263-270`
`SlotBlockingStatuses` (the *calendar* question — correct as written) and `dashboard.facade.ts:93-97`
(the *my-upcoming* question — wrong in its own way, filed separately).

### Fact 1 — `OrderStatus.Pending` has no production writer: **CONFIRMED**

Every production `AddOrderStatus` call site, read individually:

| Writer | Status |
|---|---|
| `OrderFactory.cs:166` | `New` |
| `TakeOrder.cs:194` | `Confirmed` |
| `ConfirmRecurringOrder.cs:111` | `Confirmed` |
| `HandlePaymentNotification.cs:261` | `Confirmed` |
| `NotifyOnTheWay.cs:98` | `OnTheWay` |
| `StartOrder.cs:140` | `InProgress` |
| `CompleteOrder.cs:255` | `Completed` |
| `CancelOrder.cs:133` · `AdminCancelOrder.cs:104` · `HandlePaymentNotification.cs:304` · `StaleOrderCleanupService.cs:46` · `CleanupStalePendingOrders.cs:77` · `AutoCancelStaleRecurringOrders.cs:86` | `Cancelled` |
| `AdminOverrideOrderStatus.cs:108` | `command.TargetStatus` — generic; `Pending` is a legal target per `:56-64` |

No writer emits `Pending`. `{Pending, Confirmed}` ≡ `{Confirmed}`. Confirmed as stated.

### Fact 2 — a cash order stays `New` forever: **CONFIRMED**

`OrderPaymentDispatcher.cs:59-69` — the Cash branch enqueues `GenerateReceipt` and **writes no status
track**. The only writer that can move a cash order off `New` is `TakeOrder.cs:192-194`. So for cash,
**the take is the confirmation**, and the dashboard count (surface #2, ≡ `{Confirmed}`) is
structurally zero for a pipeline of untaken cash orders while the Available pane beside it lists them.
Confirmed as stated, and this is the strongest single argument that `New` must be offerable.

### Fact 3 — a `Cancelled`/`Completed` order with a free seat is takeable: **CONFIRMED, mechanism verified**

- Order-side gates are only `ExistsAsync` (id) and `HasAvailableSpotsAsync` (`TakeOrder.cs:42-45`).
- `Order.cs:116-117`: `AvailableSpots => MaxEmployees - _assignedEmployees.Count`; `HasAvailableSpots
  => AvailableSpots > 0`. `Order.cs:519`: `MaxEmployees = RequiredEmployees + 1`.
- `Order.cs:482-491` `AddAssignedEmployee` throws **only** on no-spots — status-blind.
- The handler's status write is conditional (`TakeOrder.cs:192`), so taking a `Cancelled` order adds
  the assignment and writes no track.
- **The weekly cap is status-blind**: `OrderRepository.cs:254-258` counts assignments in the week
  window with **no status filter** — a dead job consumes one of the cleaner's 3/6/10 slots.
- **The calendar is not blocked**: `SlotBlockingStatuses` (`OrderRepository.cs:263-270`) excludes
  `Cancelled`/`Completed`, so the dead assignment does not even reserve the time.

Confirmed in full, including both halves of the "counts against the cap but does not block the
calendar" claim. The realistic path is the stale client, as the brief states.

### **REFUTED** — "`StaleOrderCleanupService` cancels abandoned card orders after 30 minutes"

This premise, offered as the reason to fear offering `New` card orders, is false **twice over**:

1. **Its predicate is unsatisfiable.** `StaleOrderCleanupService.cs:34` requires
   `o.OrderStatusHistory.Any(h => h.Status == OrderStatus.Pending)`. Per Fact 1 nothing writes
   `Pending`. The `WHERE` can never match a production row.
2. **It has no caller.** `rg` for `IStaleOrderCleanupService|StaleOrderCleanupService` across `src/`
   returns exactly two files: the class and its own interface. No DI registration, no Function, no
   hosted service.

**The sweep that actually runs is `CleanupStalePendingOrders`** — timer-triggered every 15 minutes
(`AUDIT-2026-06-01-slice-reports.md:870`) via `CleanupStalePendingOrdersHandler.cs:21` with
`OlderThanHours: 1`, and it keys on **`PaymentStatus.Pending && PaymentType == Card`**
(`CleanupStalePendingOrders.cs:51-53`) — **not on `OrderStatus` at all**.

This refutation does not weaken the case against offering `New` card orders — **it strengthens it**.
The abandonment window is not 30 minutes; it is up to **1h15m** (1h threshold + 15min cadence), and
an independent path (`HandlePaymentNotification.cs:294-304`, Stripe session expiry) also cancels. A
cleaner offered a `New` card order can be holding it for over an hour before it evaporates.

**And it hands us the rule.** The system's own live definition of "this card order may still
evaporate" is already written down, and it is on the **payment** axis. We do not need to invent a
discriminator; we need to *read the one that exists*.

---

## D1 — The ruling: offerability is a two-axis predicate

> **An order is offerable to a cleaner iff (a) its fulfilment axis is pre-work with a free seat, and
> (b) its money axis has already reached the state that taking it assumes.**

Cleansia stores these on two independent axes and the eight surfaces all failed by consulting only
the first:

| Axis | Column | Question it answers |
|---|---|---|
| **Fulfilment** | `Order.CurrentStatus` (`OrderStatus`) | how far has the *work* got |
| **Money** | `Order.PaymentStatus` + `Order.PaymentType` | has the *payment* resolved |

Instantiated over the enums as they exist (`OrderStatus.cs`, `PaymentType.cs:8-9`):

```
Offerable(o) ⟺ o.CurrentStatus == Confirmed
             ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash)
```

**Per status, with the reason:**

| Status | Offerable | Why |
|---|---|---|
| `New` + **Cash** | **YES** | No money is in flight — nothing can retract it. The take *is* the confirmation (`TakeOrder.cs:192-194`). Excluding it makes the cash board structurally empty (Fact 2). |
| `New` + **Card** | **NO** | Checkout is open or abandoned. `CleanupStalePendingOrders` cancels it within ~1h15m; Stripe expiry cancels it too. Offering it books a cleaner against money that has not arrived. |
| `Pending` | **NO** | Dead status — no writer (Fact 1). See D5. |
| `Confirmed` | **YES** | Card: money settled (`HandlePaymentNotification.cs:260-261` writes `Paid` + `Confirmed` together). Cash: already crewed, spare seat open. |
| `OnTheWay`, `InProgress` | **NO** | Work has begun. **No surface among the eight includes these** — uncontested; recorded so it is not re-litigated. |
| `Completed`, `Cancelled` | **NO** | Terminal. Closes Fact 3. |

**The card rule costs nothing in latency.** A card order becomes offerable the moment the webhook
lands — `HandlePaymentNotification.cs:260-261` writes `PaymentStatus.Paid` and the `Confirmed` track
in the same commit. We are not delaying card jobs; we are declining to advertise unpaid ones.

**Why the predicate keys on `PaymentType`, not `PaymentStatus`.** Every order is created
`PaymentStatus.Pending` regardless of type (`OrderFactory.cs:116`), and **a cash order never leaves
it** until the cleaner collects. So the developer's suggested `PaymentStatus != Failed` gate would
admit abandoned card orders (they are `Pending`, not `Failed`, for the whole abandonment window), and
a `PaymentStatus == Paid` gate would exclude **every cash order in the system**. `PaymentType` is the
only term that separates the two money models. This is the specific reason the developer's proposal is
**adapted rather than adopted** — the instinct (the rule is payment-qualified) is right; the proposed
column is the wrong one, and both wrong variants fail in production-visible ways.

---

## D2 — Offerable and takeable are the **same set**, evaluated at two moments

**Ruling: identical.** Not two related sets — one predicate, read-time and write-time.

The temptation is to make the takeable set wider ("be permissive at the write; the cleaner already
committed"). Reject it: the entire defect class in this ticket *is* a read/write gap. A wider take set
re-creates Fact 3. A narrower one re-creates the cash-count contradiction in mirror image — a cleaner
refused a job the board offered, for a reason the board already knew.

**The seam that makes "identical" coherent** — separate the two kinds of gate, which `TakeOrder`
currently interleaves:

| Kind | Predicate over | Examples | Shared? |
|---|---|---|---|
| **Offerability** | the **order** alone (+ seat count) | status, payment type, free seat | **YES — `OrderAvailability`** |
| **Eligibility** | the **(cleaner, order) pair** | approval, profile, weekly cap, time conflict, already-assigned, the ADR-0036 hold | **NO — stays per-caller in `TakeOrder`** |

Offerability is a property of the order, so it is expressible in a `WHERE` clause on every read
surface *and* in the write gate. Eligibility is per-caller: the digest and the board already apply
parts of it (country, overlap, exclude-self), and the take applies all of it. **Only the offerability
half is centralized.** This is what keeps `OrderAvailability` a small, honest role instead of a god
object, and it is why the take gate does not need to duplicate the six cleaner-side rules.

---

## D3 — The single source of truth: `Cleansia.Core.Domain.Orders.OrderAvailability`

**The developer's proposed location is correct; the proposed shape is not.**

`Cleansia.Core.Domain` is reachable from every backend call site — `DashboardSpecifications.cs:1-2`
imports `Domain.Enums` + `Domain.Specifications`; `NewJobsDigestService.cs:3` imports `Domain.Enums`;
`OrderSpecification` *is* Domain. No new project reference. Adopted.

But `OfferableStatuses` alone **cannot express the ruling** — D1's predicate is payment-qualified, so
a bare `OrderStatus[]` is exactly the under-powered artifact that produced six disagreeing lists.
It ships as **three members with distinct jobs**:

```csharp
namespace Cleansia.Core.Domain.Orders;

public static class OrderAvailability
{
    /// The COARSE fulfilment-axis floor: the statuses that can ever be offerable.
    /// NOT the rule — `New` is conditional (see IsOfferable). Exists because the
    /// clients cannot evaluate the full predicate and because it is the
    /// index-served prefilter on Orders.CurrentStatus.
    public static readonly IReadOnlyList<OrderStatus> OfferableStatuses = [OrderStatus.New, OrderStatus.Confirmed];

    /// Queryable form — composed into OrderSpecification. Total over NULL CurrentStatus.
    public static Expression<Func<Order, bool>> IsOfferableSql { get; }

    /// In-memory form — the TakeOrder write gate. Same rule, C# semantics.
    public static bool IsOfferable(OrderStatus? currentStatus, PaymentType paymentType);
}
```

**Two evaluation forms, not one shared expression — and this is deliberate.** ADR-0036 ruled exactly
this for the hold predicate (living doc `preferred-cleaner-dispatch.md:107-109`): SQL and C# disagree
on null semantics, `.Compile()` on a request path is **banned**, and the two forms are pinned by an
equivalence test rather than unified. This ADR **follows that precedent rather than contradicting it**
— our predicate has the same `NULL` hazard (`Order.CurrentStatus` is `OrderStatus?`, with pre-backfill
NULL rows documented at `OrderSpecification.cs:112-114`). The equivalence test is `TC-AVAIL-EQUIV`,
modeled on `TC-PREF-EQUIV-0`, run against real PostgreSQL.

**NULL `CurrentStatus` — ruled explicitly, because the two forms must not diverge here.** Read
surfaces today fail closed on NULL (`OrderSpecification.cs:115-116`). The take gate **must not** fail
closed identically — that would make every legacy order permanently untakeable. It resolves status the
way `HasOverlappingOrderAsync` already does (`OrderRepository.cs:285-288`): `CurrentStatus` when
non-null, else the latest history row by `(CreatedOn desc, Sequence desc)`. This also removes a latent
production NRE: `OrderMappers.cs:14-17` `GetCurrentOrderStatus()` is `order.CurrentStatus!.Value`, and
`TakeOrder.cs:191` dereferences it on the request path — a NULL row is a 500 today.

---

## D4 — Which of the eight surfaces is wrong, and what each becomes

| # | Surface | Verdict | Becomes |
|---|---|---|---|
| 1 | `NewJobsDigestService.cs:52-53` | **WRONG** — carries `Pending` (dead) and `New` unqualified | Delete the local array **and the "Mirrors" comment**. Compose `OrderAvailability.IsOfferableSql` into the existing `Where`. **T-0530 AC2 is satisfied by the strong branch: the comment is deleted, not amended** — the two surfaces stop being two. |
| 2 | `DashboardSpecifications.cs:24` | **WRONG** — ≡ `{Confirmed}`; structurally zero for cash | `orderStatuses: OrderAvailability.OfferableStatuses` **plus** the payment qualifier via the shared SQL form. Fixes both consumers at once: `GetDashboardStats.cs:236` (the count) and `GetAvailableJobsPreview.cs:50` (the preview). Also delete the false comment at `GetAvailableJobsPreview.cs:46-49` (*"matches the Available Orders tab on mobile: Pending or Confirmed"* — mobile uses `{New, Confirmed}`; **a third false mirror**, in the blast radius of the spec being changed, so it is in scope here and **not** the out-of-scope repo-wide sweep). |
| 3 | `GetPagedOrders.cs:87` | **WRONG, fixed at #4** | No direct change. A blanket floor here would break My-Completed (`{Completed}`) and admin. |
| 4 | `OrderSpecification.cs:134-139` | **WRONG — the important one** | `assigned-to-me OR (has-free-seat AND offerable)`. One change gives every non-admin browse an authoritative server floor whatever the client asks for; "my orders" panes and admin (`restrictToEmployeeId: null`) are untouched. Client status lists become a **display refinement, not a security boundary** (S1 server-truth). |
| 5 | `orders.facade.ts:142-146` (web) | **WRONG** — carries dead `Pending` | `[OrderStatus.New, OrderStatus.Confirmed]` |
| 6 | `OrdersListViewModel.kt:248` (Android) | **RIGHT** | Unchanged. `{_0, _2}` already equals the canonical floor. |
| 7 | `OrdersListLogic.swift:78` (iOS) | **RIGHT** | Unchanged. |
| 8 | `TakeOrder.Validator:38-60` | **WRONG** | Gains the gate — D6. |

**Two clients out of three were already right, and the majority set was wrong.** Had this been decided
by majority (`{New, Pending, Confirmed}`, 2 of 6 surfaces, plus the dead `Pending` in 3 of 6), the
result would have shipped a dead status to production and left Fact 3 open. Recorded because T-0530
explicitly asked not to pick the majority.

> **Amendment note, 2026-08-03 — rows 6 and 7 are right about STATUS and wrong about SEATS.** This
> table's verdicts are about the **status literal** only, and `{New, Confirmed}` on Android and iOS is
> still correct after **§D9**. What §D9 changes on those two clients is a **different parameter on the
> same call**: `isUnassigned: true` → `hasAvailableSpots: true` **+ `excludeEmployeeId: <own id>`**.
> **Both clients therefore DO change** — do not read "RIGHT / Unchanged" above as "no mobile work".



**The clients do not import the rule** — they cannot evaluate the payment qualifier (they do not filter
on `PaymentType`), and they do not need to: after #4 the server will not return a non-offerable row to
a browsing cleaner regardless of the client's list. The client lists are kept aligned to the coarse
floor by the parity test in D7, not by trust.

---

## D5 — `OrderStatus.Pending` is dead. The documented lifecycle is wrong, not unimplemented

T-0530 asks: dead status, or missing writer? **Dead — and the intent it names is already implemented
elsewhere.**

`CLAUDE.md` documents *"`Pending`: Card payment initiated (waiting for Stripe webhook)"*. That state
is real and the system does track it — on the **payment axis**: `PaymentType.Card` +
`PaymentStatus.Pending`, set at `OrderFactory.cs:116`. That pair is what the **live** abandonment sweep
keys on (`CleanupStalePendingOrders.cs:51-53`), what the Stripe expiry path checks
(`HandlePaymentNotification.cs:297`), and what this ADR's own rule reads.

So `OrderStatus.Pending` is **a fulfilment-axis name for a payment-axis fact**. It is not a missing
writer — it is a duplicate representation that was never built because the real one already existed.
Adding a writer would create two sources of truth for one fact, and every reader would then have to
know which one is authoritative. **Declare it dead.**

**Dead, not deleted.** Do not remove the enum member:
- `OrderStatus` is `[SwaggerEnumAsInt]` and the integer is on the wire to three generated clients.
- `AdminOverrideOrderStatus.cs:56-64` lists it as a legal target today, so historical rows may exist
  (DEV in particular).
- Existing readers must keep **tolerating** it: `SlotBlockingStatuses` (`OrderRepository.cs:266`) and
  `GdprDeletionService.cs:92` treat a `Pending` row as live/active — the conservative direction, and
  correct for a legacy row.

**Actions:**
1. Remove `Pending` from every *offerable/available* set (surfaces #1, #2, #5). Done by D4.
2. Remove it from `AdminOverrideOrderStatus.cs:56-64` so **no new writer can appear**. The array is a
   forward-only ordered walk and `Pending` sits between `New` and `Confirmed`, so removing it leaves
   every other transition forward — the implementer confirms the index semantics before landing.
3. Mark the member deprecated in `OrderStatus.cs` with an XML doc citing this ADR.
4. **`CLAUDE.md`'s Order Lifecycle section is wrong and must be corrected** — routed to the docs agent
   (§Escalations). This ADR does not edit `CLAUDE.md`.
5. **Delete `StaleOrderCleanupService` + `IStaleOrderCleanupService`.** Unsatisfiable predicate, zero
   callers. Leaving it is the class-level form of the exact disease T-0530 exists to kill: *an
   artifact that asserts a safety net which does not exist.* It cost this ruling a false premise; it
   will cost the next reader more. Filed separately (§Escalations) — it is not a status-set change.

---

## D6 — The take gate **ships**

**Ruling: yes.** `TakeOrder` rejects an order that is not offerable at command time.

**Why it ships, on evidence:**
1. **It is the only unguarded command in its own family.** `StartOrder.cs:47` and
   `NotifyOnTheWay.cs:49` both gate on current status. `TakeOrder` — *the one command that assigns a
   human to a job* — is the only cleaner-facing order command with no status rule. This is closing the
   family's one hole, not inventing a rule.
2. **Fact 3 is a live capacity bug**, not a theoretical one: a stale client take burns one of the
   cleaner's 3/6/10 weekly slots (`OrderRepository.cs:254-258`, status-blind) on a dead job that does
   not even block their calendar.
3. **The file is being opened anyway** — ADR-0036's T-0515 adds the hold rule inside this validator's
   existence check.

**Yes, this changes shipped behaviour, and here is the exact bill** (stating it so the implementer does
not discover it):

- **New constant** in `BusinessErrorMessage.cs`, in the `order.*` block near `:70-80`:
  `public const string OrderNotTakeable = "order.not_takeable";`
- **15 strings across 11 files** — the brief's estimate of "five locale entries" is the count for one
  client; traced against the existing `order.weekly_limit_reached` key, a partner-facing take error
  needs:
  - web partner — `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` (5 files)
  - Android partner — `partner-app/src/main/res/values{,-cs,-sk,-uk,-ru}/strings.xml` (5 files)
  - iOS — `CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings` (1 file, 5 languages)
  - **not** the admin or customer apps — `TakeOrder` is partner-only.
- **`error-contract-parity.spec.ts` will not catch a miss** — it is scoped to the *customer* app
  (`:27-30`). The partner clients have no equivalent guard. See D7.
- **No migration. No NSwag regen.** No DTO or endpoint shape changes.

**Why a new key and not an existing one:**

| Candidate | Rejected because |
|---|---|
| `OrderNotConfirmed` (`StartOrder`/`NotifyOnTheWay`) | Semantically wrong — a `New` **cash** order is takeable while *un*confirmed. The message would contradict the rule. |
| `NoAvailableSpots` | Lies. The seat exists; the order is dead. |
| `OrderNotFound` | **Reserved by ADR-0036** for the *hold* refusal specifically so exclusivity cannot be inferred. Overloading it here both dilutes that purpose and tells a cleaner "not found" about an order on their screen. |

**Interaction with ADR-0036 — rule ordering is load-bearing.** ADR-0036 folds the hold refusal *into
the existence rule* (`preferred-cleaner-dispatch.md:160-163`) so a held order returns `OrderNotFound`.
That rule must therefore be evaluated **before** the status rule, so a held order **never** returns
`order.not_takeable` (which would reveal that the order exists and is live — precisely the inference
ADR-0036 forbids). Under `Cascade.Stop` on `RuleFor(x => x.OrderId)`, the required order is:

```
NotEmpty → ExistsAsync (incl. ADR-0036 hold) → IsOfferable → HasAvailableSpots
```

`IsOfferable` goes **before** `HasAvailableSpots`: for a `Cancelled` order with a free seat the honest
answer is "this job is no longer available", not "no spots".

**ADR-0036's catalog rule is not violated.** It is narrow and deliberate — *never introduce an error
key that names **the exclusivity***. `order.not_takeable` names the order's own lifecycle, which the
cleaner can already observe, and reveals nothing about another cleaner's preferential status.

---

## D7 — Enforcement: a comment is not enforcement (three layers, none of them a comment)

T-0530 exists because **two surfaces claimed to mirror each other and did not**. The ruling is
worthless if it decays the same way. Three layers, weakest to strongest:

**1. Structural — delete the duplication (the only layer that cannot rot).** Surfaces #1, #2 and #4
stop holding literals and call `OrderAvailability`. A set that exists once cannot disagree with
itself. The `NewJobsDigestService.cs:49-50` comment is **deleted**, not corrected: a comment asserting
agreement between two things that are now one thing has nothing to assert. This is why T-0530 AC2's
strong branch is the one taken.

**2. Cross-stack parity test — the only layer that would have caught this drift.** The drift lives
across C#, TypeScript, Kotlin and Swift; no compiler and no single-stack linter spans it. Precedent
exists and works: `error-contract-parity.spec.ts:43-52` already parses **C# source** from a Jest spec
and locates the solution root by walking up to `Cleansia.Api.sln` (`:9-20`). Same shape:

> `available-status-parity.spec.ts` — parse `OfferableStatuses` from `OrderAvailability.cs`, then
> assert the Available-tab status literals in `orders.facade.ts`, `OrdersListViewModel.kt` and
> `OrdersListLogic.swift` equal it. One test, four languages, fails on the next divergence.

Lives with the partner orders feature; runs in the frontend CI job that already runs the customer
parity spec.

**3. `check-consistency.mjs` — cheap mechanical backstop** (`agents/knowledge/consistency.md`, backend
section): flag any `OrderStatus[]` literal outside `OrderAvailability.cs` that contains
`OrderStatus.Pending`, or that looks like an available/offerable set. Heuristic and line-based —
necessary, not sufficient, per that tool's own preamble (`:16-18`).

**Plus the two behavioural tests T-0530 AC4 requires** — the digest and the board, same fixture, same
run, one `New` cash order and one `New` card order. After layer 1 they agree trivially; the test
exists so a future edit cannot re-fork them silently. And `TC-AVAIL-EQUIV` (D3) pins the SQL and
in-memory forms against real PostgreSQL.

---

## D8 — Explicitly out of scope (named so the ruling is not blamed for them)

| Found | Why not ruled here |
|---|---|
| ~~**The seat dimension.**~~ **ANSWERED 2026-08-03 by the owner → now ruled in §D9.** *(Original text, preserved:* Web sends `hasAvailableSpots: true` (`orders.facade.ts:147`); Android (`:249`) and iOS (`:79`) send `isUnassigned: true`. So a 2-cleaner job with 1 cleaner on it **is** offered on web and **is not** on mobile. Same question ("what is offerable"), different axis.*)* | ~~A product decision (may a stranger join a partly-crewed booking?) with its own trade-off, and it interacts with ADR-0036's Invariant H. **Escalated — Q-AVAIL-01.**~~ **The owner answered it: yes.** See **§D9** — the ruling, the two client changes, the Invariant H consequence, and the seat-count question it exposes (`Q-AVAIL-03`). |
| ~~**The mobile Available tab has no date floor.**~~ **ABSORBED by §D9 — do not work this twice.** `GetPagedOrders.cs:58-61` applies the `-2h` default **only when `HasAvailableSpots == true`**. Mobile sends `isUnassigned` instead, so mobile lists past-dated available jobs; web (which also sends `cleaningDateFrom`) does not. | Filed as its own ticket on 2026-08-02 — **and D9's client switch closes it for free**, because the moment mobile sends `hasAvailableSpots: true` the server's `-2h` default fires. **The filed ticket should be closed as absorbed, not worked separately.** |
| `dashboard.facade.ts:93-97` — web "my upcoming" uses `{Pending, Confirmed, InProgress}`: contains dead `Pending` and **omits `OnTheWay`**, so a job vanishes from the web dashboard the moment the cleaner taps "On my way". Mobile MyActive uses `{Confirmed, OnTheWay, InProgress}`. | The *my-orders* question, not the *offerable* question. Same disease, different set. **Filed.** |
| `SlotBlockingStatuses` (`OrderRepository.cs:263-270`) | The *calendar* question. **Correct as written** — inspected, no change. |
| Repo-wide sweep for false "mirrors X" comments | T-0530 out-of-scope, upheld. The one exception (`GetAvailableJobsPreview.cs:46-49`) is in scope only because the spec it describes is changing under it. |

---

## D9 — The **seat axis**: `Q-AVAIL-01` is answered — a partly-staffed job stays offerable (added 2026-08-03, owner instruction)

**The owner's words, verbatim, 2026-08-03**, asked whether a second cleaner may join a job that already
has one:

> *"Yup, there is a possibility that he can based on the calculations of how much work there is"*

### D9.1 — The ruling

> **An order with at least one open seat remains offerable, whether or not a cleaner is already on it.
> The seat term is `AssignedEmployees.Count < <the order's seat cap>` — never `Count == 0`.**

This is a **second conjunct** beside D1's status rule, exactly as D8 predicted, and it composes with
ADR-0036's visibility conjunct without touching either:

```
offered(o, cleaner) ⟺ IsOfferable(o)                        -- D1, this ADR: is it live work?
                    ∧ o.AssignedEmployees.Count < seatCap    -- D9,  this section: is there a seat?
                    ∧ OrderVisibility.NotHeldFrom(o, cleaner)-- ADR-0036: is it open to THIS cleaner?
```

**Web is right; both mobile clients are wrong.** `orders.facade.ts:147` sends
`hasAvailableSpots: true`; Android `OrdersListViewModel.kt:246-251` and iOS `OrdersListLogic.swift:76-85`
send `isUnassigned: true`, which `OrderSpecification.cs:119-122` expands to
`AssignedEmployees.Count == 0`. **A partly-staffed job has been invisible on both mobile Available tabs
since they shipped.**

### D9.2 — Three consequences of the client switch, all of them load-bearing

**(a) It closes a separately-filed defect for free.** `GetPagedOrders.cs:58-61` applies the `-2h`
`cleaningDateFrom` default **only when `HasAvailableSpots == true`**. Today mobile sends `isUnassigned`,
so it gets no floor and lists past-dated jobs — the defect D8 filed. **The moment mobile sends
`hasAvailableSpots: true`, the floor fires.** One change, two fixes; the filed ticket is absorbed, not
worked twice.

**(b) The clients MUST also start sending `excludeEmployeeId`, or the switch ships a new bug.**
`isUnassigned: true` excluded the caller's own jobs *incidentally* (a job you are on has
`Count > 0`). `hasAvailableSpots: true` does not — and the server's `RestrictToEmployeeId` floor is
`assigned-to-me OR has-a-free-seat` (`OrderSpecification.cs:134-139`), which **deliberately does not
exclude your own**. Web already compensates (`orders.facade.ts:148`, `excludeEmployeeId: employeeId`).
Without it, a mobile cleaner's Available tab would list jobs they are **already on** — every one of
which carries a spare seat — and tapping one returns `TakeOrder.cs:55`'s already-assigned refusal.

> **The mobile Available query becomes `hasAvailableSpots: true` + `excludeEmployeeId: <own id>`.
> The two are one change, not two. Shipping the first without the second is a regression.**

**No backend change and no NSwag regen for any of this.** Both parameters already exist on the
endpoint — `Filter.HasAvailableSpots` and `Filter.ExcludeEmployeeId` are in the shipped partner-mobile
OpenAPI document (`cleansia_android/openapi/partner-mobile-api.json:1128,1142`), so the generated
clients already carry them; the mobile change is which named arguments the query builders pass
(`OrdersRepository.kt:205-222`, `PartnerOrderClient.swift:83-101`, plus their `OrderPageQuery` /
`getPaged` parameter lists).

**(c) It makes ADR-0036's Invariant H true on mobile, where it was not.** Invariant H is stated **per
seat**: *≥90% of every seat's fill window is open to the entire board*. `isUnassigned: true` withheld
**100% of every second seat's fill window from the entire mobile board, permanently, on every order** —
a strictly larger version of the very defect ADR-0036 CH-V4 caught in its own draft (a spare seat
locked after the perk had been delivered). The owner's answer removes it. **Recorded because it means
D9 is not merely compatible with ADR-0036 — it is a precondition of ADR-0036's headline invariant
holding on two of the three clients.**

### D9.3 — *"based on the calculations of how much work there is"* maps onto `RequiredEmployees`, **not** onto `MaxEmployees`

The owner's qualifier names a specific existing computation, and the codebase has exactly one:

```csharp
// Order.cs:509-522 — the ONLY work→people calculation in the system.
RequiredEmployees = (int)Math.Ceiling((double)EstimatedTime / StandardWorkUnitMinutes /* 120 */);
MaxEmployees      = RequiredEmployees + 1;
```

`EstimatedTime` is the sum of the booked services' and packages' estimates
(`OrderFactory.cs:145-147`). So **`RequiredEmployees` *is* "the calculation of how much work there
is."** `MaxEmployees = RequiredEmployees + 1` is **not** that calculation — it is a bare `+1` with:

- **no comment and no recorded rationale** anywhere in the repo;
- **no production caller of the override** — `Order.SetMaxEmployees` (`:524-533`) is invoked only from
  **four test files** (`AdminReassignOrderHandlerTests.cs:65`, `CancellationAcceptanceSignalTests.cs:356`,
  `OrderListProjectionEquivalenceTests.cs:102`, `ValidatorTestHelpers.cs:92`) and **never by a handler**,
  so `RequiredEmployees + 1` is the value every production order carries;
- **an unused sibling that names the concept it isn't**: `Order.IsFullyAssigned => Count >=
  RequiredEmployees` (`:118`) — *"the work is covered"* — is **read by nothing in production**.

**And the `+1` is not free.** `CalculateOrderPay` writes **one `OrderEmployeePay` per assigned
employee** (`:140-152`), and `PayCalculatorExtensions.CalculateAggregatedPay` (`:30-61`) has **no
crew-size term at all** — `basePay` is the full per-order rate for *every* cleaner on the order. So
**each seat filled beyond `RequiredEmployees` costs a second full labour payment against the same
customer price.** On the modal booking (`EstimatedTime ≤ 120` ⇒ `RequiredEmployees = 1`,
`MaxEmployees = 2`) the spare seat can **double the labour cost of work that needs one person**.

### D9.4 — What is ruled here, and what is escalated

**Ruled (architecture), and it is what makes the escalation cheap:**

> **There is ONE seat cap, it is a property of `Order`, and every surface reads it. No surface
> re-derives it, and no surface substitutes `RequiredEmployees` for it locally.** Today that property
> is `MaxEmployees`; `OrderSpecification.cs:126,138`, `Order.AvailableSpots` (`:116`),
> `NewJobsDigestService.cs:101` and `Order.AddAssignedEmployee` (`:482-491`) already all read it, and
> they must keep agreeing.

**Ruled (architecture): if a spare seat is wanted, it must be a NAMED policy number, not a bare `+1`.**

```csharp
// The shape, whichever number the owner picks:
MaxEmployees = RequiredEmployees + BookingPolicy.SpareSeatsPerOrder;
```

Same treatment ADR-0036 D3 gave the hold constants and ADR-0035 D2.1 gave the plan numbers: a platform
number that is **visible, citable and tunable in one place**, instead of a literal nobody can explain.
This is worth doing **regardless of which value the owner chooses**.

**Escalated — `Q-AVAIL-03` (business, not blocking):**

> **Should an order carry seats for exactly the crew the work needs (`RequiredEmployees`), or one
> spare (`RequiredEmployees + 1`, today's shipped value)?** The owner's sentence points at the
> work-derived number; the shipped code adds one on top of it with no recorded reason; and the
> difference is **up to one extra full labour payment per order** (D9.3). This is a margin decision,
> not an architecture decision.

**Interim, so nothing is blocked: `MaxEmployees` stands, unchanged.** It is the shipped value, four
surfaces read it, and changing it is a behaviour change on the board, the digest, the dashboard and the
write gate. **And because D9.4's first ruling holds, flipping it later is a one-line change in
`Order.CalculateRequiredEmployees` — every surface reads the property, not the formula.** That is the
whole reason the property-not-formula rule is stated before the number is known.

### D9.5 — What D9 does **not** decide

- **It does not change the take gate.** `TakeOrder`'s seat check is `HasAvailableSpots`
  (`TakeOrder.cs:44`) and stays exactly as it is — offer and take keep the **same** seat term, per D2.
- **It does not introduce a "covered but not full" state.** `IsFullyAssigned` names it and is unused;
  whether a covered-but-not-full order should be offered *differently* (lower in the sort, not at all,
  only to certain cleaners) is a product question nobody has asked. **Not designed here.** It is
  recorded only because it is the vocabulary the owner's sentence needs if `Q-AVAIL-03` comes back as
  *"keep the spare seat."*
- **It does not touch the pay formula.** D9.3 cites it as *evidence about cost*; the
  `basePay/extras/expenses/clamp/bonus-deduction` formula and `EmployeePayConfig` are byte-untouched.

---

## Alternatives considered

| # | Alternative | Why not |
|---|---|---|
| A1 | **`{New, Pending, Confirmed}`** — the digest's set; the majority across clients | Ships a status with no writer, and offers unpaid card orders that a live sweep cancels within ~1h15m. |
| A2 | **`{Pending, Confirmed}`** — the dashboard's set | ≡ `{Confirmed}`. Makes the cash board **structurally empty** (Fact 2). The single most broken option, and it is the one currently governing the partner dashboard count. |
| A3 | **`{New, Confirmed}`** — the mobile set; status-only, no payment term | Closest, and it is the coarse floor we adopt. Rejected **as the whole rule** because it offers abandoned card checkouts. Kept as `OfferableStatuses` for the clients + the index prefilter. |
| A4 | **`PaymentStatus != Failed`** — the developer's first variant | Admits abandoned card orders for the entire window: they are `Pending`, not `Failed`, until the sweep runs. Gates on the symptom after the fact. |
| A5 | **`PaymentStatus == Paid`** — the strict money reading | **Excludes every cash order ever** (`OrderFactory.cs:116` creates all orders `Pending`; cash leaves it only at collection). Catastrophic; recorded because it is the obvious "safe" choice. |
| A6 | **Takeable ⊃ offerable** (permissive write) | Re-creates Fact 3. The read/write gap *is* the defect class. |
| A7 | **Takeable ⊂ offerable** (strict write) | Re-creates the cash-count contradiction in mirror image — refused for a reason the board knew. |
| A8 | **No take gate; comment the omission** (T-0530 AC3's weak branch) | Leaves a verified capacity bug live to avoid 15 strings. The gate is ~6 lines against a validator being opened anyway by T-0515. |
| A9 | **Reuse `OrderNotFound` for the take refusal** | Collides with ADR-0036's deliberate reservation of that key for the hold, and misinforms the cleaner. |
| A10 | **One shared `Expression`, `.Compile()`d for the in-memory path** | **Banned by ADR-0036** (`preferred-cleaner-dispatch.md:107-109`): SQL/C# null semantics differ and `.Compile()` on a request path is forbidden. Two forms + an equivalence test. |
| A11 | **Delete `OrderStatus.Pending`** | Wire-visible integer in three generated clients; legacy rows may exist. Deprecate + remove writers; readers keep tolerating. |
| A12 | **Add the missing `Pending` writer** (honour the documented lifecycle) | Creates a second source of truth for a fact the payment axis already owns, and every reader would need to know which wins. |
| A13 | **Fix the floor in `GetPagedOrders`** instead of `OrderSpecification` | Breaks My-Completed and admin, and leaves the `RestrictToEmployeeId` visibility hole open. #4 is the correct seam. |
| A14 | **Push the rule to the clients** (each keeps its list, aligned by review) | Exactly what produced six lists. Server floor + parity test instead. |

---

## How a reviewer verifies compliance

1. `rg "OrderStatus.Pending" src/ --type cs` — no hit inside any available/offerable set, and none in
   `AdminOverrideOrderStatus`'s `Lifecycle`.
2. `rg -n "OrderStatus\.(New|Confirmed)" src/Cleansia.Core.AppServices` — no status *set* literal for
   availability outside `OrderAvailability.cs`. **Check call sites, not hit counts** (ADR-0036's trap
   #3: `OrderSpecification.Create`'s parameters are all optional, so a caller that forgets the new
   argument compiles green and leaks).
3. `NewJobsDigestService.cs` — the `AvailableStatuses` array and the "Mirrors" comment are **gone**, not
   edited.
4. `OrderSpecification.cs` `RestrictToEmployeeId` — the free-seat disjunct carries the offerability
   conjunct. Confirm the `ExcludeEmployeeId` block is untouched (opposite polarity — ADR-0036 trap #2).
5. `TakeOrder.Validator` — `IsOfferable` sits **after** `ExistsAsync` and **before** `HasAvailableSpots`.
6. `order.not_takeable` resolves in all 11 locale files; a missing one shows the raw key.
7. The parity spec fails if any client's Available list is edited alone (flip one and watch it go red).
8. AC4's two tests run on one fixture and agree for a `New` **cash** order (offered, counted, takeable)
   and a `New` **card** order (not offered, not counted, not takeable).

**Added 2026-08-03 for §D9 (the seat axis):**

9. `rg -n "isUnassigned" src/cleansia_android src/cleansia_ios` — **no Available-tab call site sends
   it.** `OrdersListViewModel.kt` and `OrdersListLogic.swift` send `hasAvailableSpots: true`.
10. **…and every one of those call sites also sends `excludeEmployeeId`.** A diff that changes the
    first without the second is a **hard reject** — it ships a tab listing jobs the cleaner is already
    on, each of which errors on tap (`TakeOrder.cs:55`). Check the two together, in the same hunk.
11. **The date floor actually fires.** With the mobile client's own `cleaningDateFrom` unset, the
    Available tab no longer returns past-dated jobs (`GetPagedOrders.cs:58-61`'s `-2h` default). This is
    the absorbed ticket's acceptance test; run it here rather than filing it again.
12. **No surface re-derives the seat cap.** `rg -n "RequiredEmployees \+ 1|MaxEmployees" src/ --type cs`
    — the arithmetic appears **only** in `Order.CalculateRequiredEmployees`; every other hit reads the
    property. A second `+ 1` anywhere defeats the one-line flip `Q-AVAIL-03` depends on.

---

## Escalations (owner) — listed here, **not** written to `questions/open.md` by this ADR

- ~~**Q-AVAIL-01 — the seat dimension (product).**~~ **ANSWERED by the owner, 2026-08-03: YES.**
  *(Original question, preserved:* Should a partially-crewed order be offered to other cleaners? Web
  says yes, mobile says no; they have disagreed in production. Interacts with ADR-0036 Invariant H
  (which is stated *per seat*). This ADR rules the **status** axis only and leaves both behaviours as
  they are.*)* → **Ruled in §D9**: a partly-staffed order stays offerable; both mobile clients switch
  to `hasAvailableSpots` **+ `excludeEmployeeId`**; the mobile date-floor ticket is absorbed; Invariant
  H becomes true on mobile.
- **`Q-AVAIL-03` — the seat cap (business, not blocking).** Raised by §D9.3. Should an order carry
  seats for exactly the crew the work needs (`RequiredEmployees = ceil(EstimatedTime / 120)`), or one
  spare (`RequiredEmployees + 1`, today's shipped value with no recorded rationale)? Each filled seat
  beyond the required crew costs **a second full labour payment** — `CalculateOrderPay:140-152` writes
  one pay row per assigned employee and `CalculateAggregatedPay:30-61` has no crew-size term.
  **Interim: `MaxEmployees` stands unchanged**, and §D9.4's property-not-formula rule makes the flip a
  one-line change if the owner rules the other way.
- **Q-AVAIL-02 — `New` + Card, recorded as decided, flip condition named.** Ruled **not offerable** on
  the evidence above. If the business would rather cleaners pre-claim unpaid card bookings to shorten
  time-to-fill, that is an owner call with a customer-facing consequence (a cleaner assigned to a
  booking that is then cancelled for non-payment). **Flip condition:** measured time-to-first-assignment
  showing card jobs starve relative to cash.
- **Docs correction (not an escalation — a docs ticket).** `CLAUDE.md`'s Order Lifecycle documents
  `Pending` as a live card state. It is dead. Route to the docs agent with this ADR as the citation.
  Also check `docs/architecture/*.md` for the same claim.
- **Dead-code removal (a ticket, not a decision).** Delete `StaleOrderCleanupService` +
  `IStaleOrderCleanupService`.

---

## Amended by owner instruction — 2026-08-03 (`architect`, author mode) · `Q-AVAIL-01` answered

**This ADR was still `proposed` and unchallenged when the amendment landed**, so the ruling is folded
into the body as **§D9** rather than appended as a supersede — that is the sanctioned form for a
proposed ADR, and the panel that eventually reviews this document reviews D9 with the rest of it.
**Nothing was silently rewritten:** D8's out-of-scope row and the `Q-AVAIL-01` escalation are struck in
place with their original text preserved and a pointer to D9, and D4's table carries a note saying
precisely what "rows 6/7 are RIGHT" does and does not mean now.

**The owner's words, verbatim, 2026-08-03**, on whether a second cleaner may join a partly-staffed job:

> *"Yup, there is a possibility that he can based on the calculations of how much work there is"*

**What changed:** §D9 (new). **What did not:** D1–D8, the payment-qualified status rule, the
`OrderAvailability` shape, the take gate, the `Pending` ruling and every alternative above. D9 is a
**second conjunct on a different axis**, which is exactly the disposition D8 gave it when it escalated
the question.

**One new escalation falls out of the answer and is listed above: `Q-AVAIL-03`** — the owner answered
*whether* a second cleaner may join, which does not answer *how many seats a job has*. The `+1` in
`MaxEmployees = RequiredEmployees + 1` has no recorded rationale and costs a full extra labour payment
per filled spare seat; that number is the owner's, and the interim is unchanged behaviour.

**Companion updates in the same change:** `agents/architecture/decisions/order-availability.md`,
`agents/knowledge/roles/order-availability.md` (its watch-list carried `Q-AVAIL-01` as unresolved), and
**ADR-0039**, which carries the owner's *other* ruling from the same conversation (preferred-cleaner
slot availability) and cross-references `Q-AVAIL-03` so the two owner questions are findable together.

---

## Challenge

<!-- Challengers: name the specific hole (alternative dismissed too fast, seam broken, future change
     made expensive, hidden coupling, cheaper option) and why it matters, citing file:line. A
     challenger that finds nothing says so and names what they checked.

     D9 is new and unchallenged. Suggested angles the author names rather than leaves to be found:
       • Is D9.2(b) (excludeEmployeeId) really required, or does RestrictToEmployeeId already handle
         it? (Author's reading: OrderSpecification.cs:134-139 is assigned-to-me OR has-a-seat — it
         does NOT exclude. Verify.)
       • Does the -2h floor in GetPagedOrders.cs:58-61 actually fire once mobile sends
         hasAvailableSpots, or does the mobile client also need to stop relying on its own paging?
       • Is "one seat cap, read as a property" enough, or does Q-AVAIL-03's interim leave a surface
         that would silently keep the +1 semantics after a flip? -->



## Defense

<!-- Author: REBUT (with evidence) / CONCEDE + REVISE (fold the fix in above) / ESCALATE, per challenge. -->

## Verdict

<!-- Lead: every challenge RESOLVED or BLOCKING. Consensus = zero blocking. Then status → accepted. -->
