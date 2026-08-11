# ADR-0037 — Which orders a cleaner may be **offered** and which they may **take** is **one rule, evaluated at two moments**: an order is offerable iff its **fulfilment** axis is pre-work-with-a-free-seat **and** its **money** axis has already reached the state the take assumes — concretely `Confirmed`, plus `New` **only for `PaymentType.Cash`**; the rule lives once in `Cleansia.Core.Domain.Orders.OrderAvailability`; `TakeOrder` **gains a status gate** (new key `order.not_takeable`, 15 strings in 11 files); and `OrderStatus.Pending` is **declared dead** — the documented "card payment initiated" lifecycle is already implemented on the **payment** axis, so the missing writer is not missing, it is a duplicate that was never built

- **Status:** `accepted` — **2026-08-03, by the lead of the defense panel** (`## Verdict`). Drafted
  2026-08-02 (author mode, **T-0530 AC1**), amended by owner instruction 2026-08-03 (§D9), then
  **attacked by two challenger lanes — 19 findings, 8 of them marked blocking — and adjudicated
  2026-08-03**. Consensus: **zero blocking challenges remain.** Immutable from here; a deviation needs
  a superseding ADR.
- ⚠️ **AMENDED BY THE PANEL, 2026-08-03. Eleven findings were CONCEDED and the decision sections
  below are the amended text**, each change marked `[amended by CH-…]` in place so a reader sees both
  what the draft said and what the panel changed. **The two amendments that change the ruling itself:**
  **(1)** D1's predicate gains a `NotRetractable` conjunct — the draft's `Confirmed ∨ (New ∧ Cash)`
  was falsified in *both* directions by two live sweeps (CH-M3, CH-M4); **(2)** D6 no longer relies on
  FluentValidation rule ordering, which does not deliver the property it claimed (CH-M2). Everything
  else is corrected evidence, widened scope, or enforcement that actually runs.
- ⚠️ **AMENDED BY OWNER INSTRUCTION, 2026-08-03 — `Q-AVAIL-01` IS ANSWERED: a second cleaner MAY join a
  partly-staffed job.** The ruling and its consequences are **§D9**, added below; the dated amendment
  note is at the end of this file. **The status axis this ADR rules is untouched** — D9 is the *seat*
  axis, a second conjunct, exactly as D8 said it would be. Nothing above D9 has been rewritten: D8's
  out-of-scope row and the `Q-AVAIL-01` escalation are marked **ANSWERED** in place with a pointer, so
  the original framing of the question is still readable.
- **Date:** 2026-08-02 (drafted) / 2026-08-03 (amended — owner ruling on `Q-AVAIL-01`, §D9) /
  **2026-08-03 (panel amendments + accepted)**
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
- **⚠️ SCOPE GREW AT THE PANEL — re-size T-0530 before ticketing.** Added by the amendments: two more
  `OrderAvailability` parameters; **three** more surfaces (web row-action button, web detail-page Take
  button, web filter dropdown); **`TakeOrder.Validator` collapsed to one chain** + `TC-TAKE-ONE-ERROR`;
  the **web reconcile** on two facades; **three** error keys instead of one (two of them reused) plus a
  copy re-voice plus a partner-web backfill; the **date-floor constant**; an `AdminOverrideOrderStatus`
  target guard + seeded test; a `PaymentType` exhaustiveness test; and the parity check re-shaped as a
  **plain node script with its own CI workflow**. Six further defects are **not** in T-0530 —
  see §Escalations.
- **Ticket:** T-0530 (AC1 is this document; AC2/AC3/AC4 are implemented against it).
  Serialization: T-0529 → **T-0530** → T-0528 all edit `NewJobsDigestService.cs`; **T-0515**
  (ADR-0036) edits four of the same surfaces and must land after this rule exists, not beside it.

> **One decision:** *what makes an order offerable to a cleaner.* Everything else here is a corollary.
> The answer is not a status list. It is: **an order is offerable when nothing that is still in flight
> can retract it.**
>
> **[AMENDED by CH-M3 + CH-M4.]** The draft stated that invariant and then shipped a predicate that
> did not test it — it asserted "for cash nothing is in flight", which is false for a **recurring**
> cash order (a live hourly sweep retracts it at T−1h), and it trusted `Confirmed` to imply paid,
> which is false for a card order an admin pushed forward or a decline left `Pending` (a live 15-minute
> sweep retracts that one). **The predicate now literally tests the invariant**: `NotRetractable(o)` is
> the union of the negations of the two scheduled retractors that actually run, read off their own
> `WHERE` clauses. See **§D1**. *The slogan was right; the code under it was not. That gap is the same
> defect class as a comment claiming two lists agree — the class this ADR exists to close.*

---

## Why this is ADR-weight and not a one-line ruling

T-0530 scoped this as *"one architect, one item"* — reasonable when the ticket believed the divergence
was three-way and cosmetic. Verification found otherwise, and four of the findings are decision-shaped:

1. **The divergence is eight-way, not three-way** (§D0), and two of the eight are *server-side
   authorization* surfaces, not display. **[The panel found it is TEN-way — CH-X5. The two the draft
   missed are the web *buttons*, and one of them contradicts this ADR's ruling. See §D0.]**
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

### The ~~eight~~ **ten** surfaces that answer "which orders may a cleaner work on"

**[AMENDED by CH-X5 — the census was eight; it is ten, and the two that were missed are the web
*buttons*. Rows 9 and 10 are new; row 11 is a related surface the same lane's evidence exposes
(CH-X6). The draft's own thesis is that what is *listed* and what is *clickable* must not diverge — a
census that counted queries and not buttons could not see that.]**

| # | Surface | Today | Kind |
|---|---|---|---|
| 1 | `NewJobsDigestService.cs:52-53` | `{New, Pending, Confirmed}` | push |
| 2 | `DashboardSpecifications.cs:24` | `{Pending, Confirmed}` | count + preview |
| 3 | `GetPagedOrders.cs:87` | client-supplied, **no server floor** | list |
| 4 | **`OrderSpecification.cs:134-139` `RestrictToEmployeeId`** | **status-blind seat arithmetic** | **server-side visibility** |
| 5 | `orders.facade.ts:142-146` (web) | `{New, Pending, Confirmed}` | client display (**query**) |
| 6 | `OrdersListViewModel.kt:248` (Android) | `{_0, _2}` = `{New, Confirmed}` | client display |
| 7 | `OrdersListLogic.swift:78` (iOS) | `{._0, ._2}` = `{New, Confirmed}` | client display |
| 8 | **`TakeOrder.Validator` `:38-60`** | **no status rule** | **write gate** |
| **9** | **`orders.models.ts:169-176` (web, Available row action)** | `{New, Pending, Confirmed}` ∧ `availableSpots > 0` | **client display (BUTTON)** |
| **10** | **`order-details.helpers.ts:108-115` `canTakeOrder` (web, detail page)** | `{Pending, Confirmed}` ≡ **`{Confirmed}`** | **client display (BUTTON)** |
| *11* | *`orders.helpers.ts:46-57` `buildOrderStatusOptions` (web filter dropdown)* | *offers dead `Pending`, **omits `New`*** | *client filter vocabulary* |

**#10 is the surface that contradicts the ruling in the direction that hides work.** Verified at
source: `canTakeOrder` gates on `Pending || Confirmed`, and `Pending` has no writer (Fact 1), so it is
`{Confirmed}`. A `New` **cash** order — the case Fact 2 calls the strongest single argument that `New`
must be offerable — is **listed** on the web board, is **takeable** by the server, and its detail page
shows **no Take button**. Both mobile clients get it right (`OrderPrimaryAction.swift:44-48`,
`OrderPrimaryAction.kt:57-58`), which is what makes this a web defect rather than a design choice.

**#11 is a cliff, not a gap** (CH-X6, verified at `orders.helpers.ts:49-56`): the dropdown offers
`Pending` (structurally empty — nothing writes it) and does **not** offer `New`, while
`orders.facade.ts:142` reads `additionalFilters?.orderStatuses || [...]` — so the moment a cleaner
touches the filter *at all*, their selection **replaces** the default list and every `New` cash job
leaves the board, with no dropdown option that brings it back. After this ADR that gets worse, not
better: `New` + Cash becomes the canonical pre-take state of the entire cash pipeline and is the one
state the filter cannot express.

**#4 is the surface nobody counted, and it is the one that matters most.** For a non-admin caller
`GetPagedOrders.cs:91` pins `restrictToEmployeeId`, and `OrderSpecification.cs:136-138` expands it to:

```csharp
x.AssignedEmployees.Any(ae => ae.EmployeeId == RestrictToEmployeeId)
|| x.AssignedEmployees.Count < x.MaxEmployees
```

The second disjunct is **pure seat arithmetic with no status term**. This is the server's *only*
authoritative floor on what a browsing cleaner may read, and it admits every `Cancelled` and
`Completed` order that has a free seat. Surface #3's missing floor is therefore not a separate defect:
**#3 has no floor because #4 is the floor, and #4 is status-blind.** One fix closes both.

> **[CORRECTED by CH-M8a — the draft said "which, per `Order.cs:519` (`MaxEmployees =
> RequiredEmployees + 1`), is nearly all of them." That magnitude is now false and the citation is
> stale.]** The owner's `Q-AVAIL-03` ruling shipped: `BookingPolicy.cs:76` `SpareSeatsPerOrder = 0`,
> and the formula moved into `Order.CalculateRequiredEmployees` (`Order.cs:534`). At cap =
> `RequiredEmployees`, a **fulfilled** order has zero free seats — neither cancel nor complete
> unassigns anyone (`Order.UnassignEmployee` has exactly **one** production caller,
> `AdminReassignOrder.cs:86` — re-verified) — so a fully-crewed `Completed` order is already invisible
> to the `RestrictToEmployeeId` floor. **What survives is `Cancelled`-before-anyone-took-it (common)
> and under-crewed multi-seat orders.** *Fact 3 survives in full; only its blast radius shrinks.* This
> matters because D6's justification #2 quotes the severity, and an implementer who catches one
> falsified premise stops trusting the other forty citations.

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

### Fact 2 — a **one-off** cash order stays `New` forever: **CONFIRMED, categorical corrected**

`OrderPaymentDispatcher.cs:59-69` — the Cash branch enqueues `GenerateReceipt` and **writes no status
track**. So for a one-off cash order **the take is the confirmation**, and the dashboard count
(surface #2, ≡ `{Confirmed}`) is structurally zero for a pipeline of untaken cash orders while the
Available pane beside it lists them. That is the strongest single argument that `New` must be
offerable, and it stands.

> **[CORRECTED by CH-M3 — the draft's categorical "The only writer that can move a cash order off
> `New` is `TakeOrder.cs:192-194`" is false.]** `ConfirmRecurringOrder.HandleCashAsync`
> (`:111-112`) also does: for a **recurring** cash order the customer's own confirm writes
> `Confirmed` + `PaymentStatus.Paid` with **no cleaner assigned**. This is not pedantry — it has two
> load-bearing consequences: it is the discriminator D1's new `NotRetractable` term keys on
> (`PaymentStatus == Paid` *is* "this recurring occurrence is confirmed"), and it is the **real**
> generator of an offerable `Confirmed` cash order with an empty crew, which the draft's D1 attributed
> to a "spare seat" that no longer exists (CH-M8b). In a document whose authority rests on *"Gate 0:
> every row read, not inherited"*, a wrong categorical in the fact table is corrosive — recorded, not
> quietly fixed.

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
> (b) its money axis has already reached the state that taking it assumes — i.e. no scheduled job can
> still retract it.** *(clause (b) restated by the panel: "the state the take assumes" was the
> intention; "nothing can retract it" is the testable form.)*

Cleansia stores these on two independent axes and all ten surfaces failed by consulting only
the first:

| Axis | Column | Question it answers |
|---|---|---|
| **Fulfilment** | `Order.CurrentStatus` (`OrderStatus`) | how far has the *work* got |
| **Money** | `Order.PaymentType` (the *model*) + `Order.PaymentStatus` (the *progress*) + `Order.RecurringTemplateId` (which sweep applies) | has the *payment* resolved, **and can anything undo it** |

Instantiated over the enums as they exist (`OrderStatus.cs`, `PaymentType.cs:8-9`):

> **[AMENDED by CH-M3 + CH-M4 — this is the one place the panel changed the ruling itself.]** The
> draft's predicate was `Confirmed ∨ (New ∧ Cash)`. Two scheduled sweeps that run in production
> falsify it **in both directions**: one admits an order that gets retracted an hour before the
> cleaning (`New` + Cash + recurring), the other admits an order that gets retracted 15 minutes from
> now (`Confirmed` + Card + unpaid). The draft form is preserved in the `## Defense` entries for
> CH-M3/CH-M4. **The status term is unchanged; a second conjunct is added.**

```
Offerable(o) ⟺ ( o.CurrentStatus == Confirmed
               ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash) )   -- the STATUS term (draft, unchanged)
             ∧ NotRetractable(o)                                       -- the MONEY term (added by the panel)

NotRetractable(o) ⟺ o.PaymentStatus == PaymentStatus.Paid
                  ∨ (o.PaymentType == PaymentType.Cash ∧ o.RecurringTemplateId == null)
```

**`NotRetractable` is the pull-quote invariant promoted from prose to code**, and it is derived by
this ADR's own method — *read the discriminator the system already wrote down*. There are exactly two
scheduled retractors in production; the term is the union of their negations, read off their own
`WHERE` clauses:

| Retractor | Its predicate (verified) | Why `NotRetractable` excludes its victims |
|---|---|---|
| `CleanupStalePendingOrders.cs:50-53` — 15-min timer, `OlderThanHours: 1` | `PaymentStatus == Pending ∧ PaymentType == Card ∧ CreatedOn < now−1h` — **no `OrderStatus` term at all** | a card order survives only via `PaymentStatus == Paid`, which `HandlePaymentNotification.cs:260-261` writes in the same commit as `Confirmed` |
| `AutoCancelStaleRecurringOrders.cs:63-69` — hourly, grace 1h | `RecurringTemplateId != null ∧ PaymentStatus == Pending ∧ CleaningDateTime <= now+1h ∧ UserId != null` — **no `PaymentType` term at all** | a recurring order survives only via `PaymentStatus == Paid`, which `ConfirmRecurringOrder.cs:111-112` writes when the customer confirms (cash included) |

**Per case, with the reason:**

| Case | Offerable | Why |
|---|---|---|
| `New` + Cash, **one-off** | **YES** | No retractor matches. The take *is* the confirmation (`TakeOrder.cs:192-194`). Excluding it makes the cash board structurally empty (Fact 2). |
| `New` + Cash, **recurring**, `PaymentStatus.Pending` | **NO — [added by CH-M3]** | `AutoCancelStaleRecurringOrders` cancels it at **T−1h**, up to **7 days** after the materializer created it (`MaterializeRecurringBookings.cs:27,131`). On a recurring order `PaymentStatus == Pending` *is* "the customer has not confirmed this occurrence" — **the sweep is correct and is not changed here; the draft's rule was wrong.** This is a *worse* cleaner experience than the card case the draft refuses: the retraction lands an hour before the slot, not an hour after creation, and the sweep's own doc-comment (`:25-27`) says it exists to "free the cleaner's slot". |
| `Confirmed` + Cash, **recurring**, `PaymentStatus.Paid` | **YES** | `ConfirmRecurringOrder.HandleCashAsync` (`:111-112`) writes `Confirmed` + `Paid` with **no cleaner assigned**. This — not a "spare seat" — is the real generator of an offerable `Confirmed` cash order (**[reason corrected by CH-M8b]**). |
| `New` + Card | **NO** | Checkout is open or abandoned. `CleanupStalePendingOrders` cancels it within ~1h15m; Stripe expiry cancels it too. Offering it books a cleaner against money that has not arrived. |
| `Confirmed` + Card, `PaymentStatus.Paid` | **YES** | Money settled in the same commit as the status. |
| `Confirmed` + Card, `PaymentStatus.Pending` | **NO — [added by CH-M4]** | **Reachable, two ways.** `AdminOverrideOrderStatus.Handler` is the generic status writer and has **no payment guard** — its only checks are terminal-state (`:83-94`) and forward-rank (`:96-106`), and "customer says they paid, the webhook never landed, push it to Confirmed" is exactly what an override is for. And a **declined** card is *deliberately* left `PaymentStatus.Pending` so the client can retry (`HandlePaymentNotification.cs:230-242`), so the unpaid-card population is larger than "abandoned checkouts". The 15-min sweep has no `OrderStatus` term, so it cancels this order out from under an already-assigned cleaner. |
| `Pending` | **NO** | Dead status — no writer (Fact 1). See D5. |
| `OnTheWay`, `InProgress` | **NO** | Work has begun. **No surface among the ten includes these** — uncontested; recorded so it is not re-litigated. |
| `Completed` | **NO** | Terminal. Closes Fact 3. |
| `Cancelled` | **NO** | **[QUALIFIED by CH-X8b]** Terminal **for this predicate at the moment it is evaluated** — *not* terminal in the lifecycle. `HandleCompletedSession` short-circuits only on `PaymentStatus is Paid or Refunded` (`HandlePaymentNotification.cs:254`) and the sweep leaves `Failed`, so a customer who pays late produces a real `New → Cancelled → Confirmed` + `Paid` history. That order **correctly re-enters** the offerable set: it is a live, paid job again, the predicate is stateless and simply re-evaluates on the *latest* track. Do **not** read "terminal" as "an order that has ever been `Cancelled` is permanently excluded" — no surface may cache that. |

**The asymmetry mattered more than either row.** The draft payment-qualified `New` and *trusted*
`Confirmed` to imply paid. `Confirmed` has four writers and only one of them
(`HandlePaymentNotification.cs:260-261`) writes the money in the same commit. **A rule that trusts a
status to imply a fact stored in a different column is the same defect class as a comment claiming two
lists agree** — and this ADR exists to kill that class. Symmetry is not tidiness here; it is the
difference between a rule and a habit.

**The card rule costs nothing in latency.** A card order becomes offerable the moment the webhook
lands — `HandlePaymentNotification.cs:260-261` writes `PaymentStatus.Paid` and the `Confirmed` track
in the same commit. We are not delaying card jobs; we are declining to advertise unpaid ones.

**Why the STATUS term keys on `PaymentType` and the MONEY term keys on `PaymentStatus`** — they answer
different questions and neither column can answer both. `PaymentType` is the money **model**: it says
whether money is expected *before* the work (card) or *at* the work (cash), which is what makes `New`
admissible at all. `PaymentStatus` is the money **progress**: it is what the two live sweeps actually
read, so it is the only column that can answer "will something retract this". The developer's original
proposal is therefore **adapted, not adopted, and now adapted in both halves**: `PaymentStatus !=
Failed` and `PaymentStatus == Paid` are still rejected **as whole rules** (A4, A5) for exactly the
reasons given — but the panel's finding is that neither rejection ever addressed the **conjunction**,
which is a different proposition and is what ships. See A15/A16.

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
    /// [AMENDED by CH-M3 + CH-M4] The signature carries the money axis, not just
    /// the money MODEL: the two live sweeps read PaymentStatus and
    /// RecurringTemplateId, so a predicate without them cannot state the ADR's own
    /// invariant. Four scalars, no navigation properties, no I/O — the role stays
    /// a pure function of the order's own columns.
    public static bool IsOfferable(
        OrderStatus? currentStatus,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId);
}
```

**The added parameters do not widen the role — they narrow what it may believe.** All four are
columns on `Order`; none is a property of a cleaner, and none requires a collaborator. The role card
(`agents/knowledge/roles/order-availability.md`) previously listed *"Payment state — it reads
`PaymentType`, never `PaymentStatus`"* under **does NOT know**; that line is **struck by this
amendment**, because the scenario CH-M3/CH-M4 produced is exactly the case the RDD rule anticipates:
*if a scenario forces a role to know something on its "does NOT know" list, the responsibility was
wrong.* It was. Availability is not "what money model is this" — it is **"can anything take this order
away from the cleaner I hand it to"**, and that question cannot be answered without the progress
column. What stays on the list, unchanged and load-bearing: **anything about a cleaner.**

**Extension obligation — [added by CH-M9].** `PaymentType` is `{ Cash = 1, Card = 2 }` today. The
rule fails *safe* on a new member (a `New` order of an unknown type is not offerable), but it fails
**silently** and wrongly for a pay-on-site type such as `Invoice` (B2B, settles after the job —
semantically cash). The codebase already knows this enum grows: `OrderPaymentDispatcher.cs:71-72` and
`ConfirmRecurringOrder.cs:100-101` both carry `default:` arms. **No abstraction is introduced** —
switching on `PaymentType` is idiomatic here. Instead, `OrderAvailability` carries an **exhaustiveness
test over `Enum.GetValues<PaymentType>()`** (natural home: beside `TC-AVAIL-EQUIV`) that goes **red on
a new member until `OrderAvailability` explicitly classifies it on *both* axes** — offerable-at-`New`?
and retractable-by-which-sweep? Same "a comment is not enforcement" standard D7 applies to everything
else, at the cost of one test.

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
`TakeOrder.cs:191` dereferences it on the request path.

> **[CORRECTED by CH-M10 — the design is right; the evidence for it was overstated.]** The draft
> ended that sentence *"— **a NULL row is a 500 today**."* It is not. `Order.CurrentStatus`
> (`Order.cs:284-289`) **already** falls back to the loaded history
> (`OrderByDescending(CreatedOn).ThenByDescending(Sequence)`), `TakeOrder.Handler` includes that
> history (`:179`), and every order gets a `New` track at creation (`OrderFactory.cs:166`). **The 500
> therefore requires a NULL column *and* zero loaded history rows** — a much smaller population. The
> asymmetry this paragraph rules (reads fail closed on NULL, the write gate must **not**, or every
> legacy order becomes permanently untakeable) is genuinely non-obvious and is **kept exactly as
> drafted**. D3 is the section an implementer reads hardest; a wrong claim here costs more than
> elsewhere.

**The two new terms are NULL-safe by construction, and the equivalence test must say so.**
`PaymentStatus` and `PaymentType` are non-nullable enum columns; `RecurringTemplateId` is a nullable
`string`, and `RecurringTemplateId == null` is the *only* three-valued term in the predicate — it is
compared to `NULL` directly, which EF Core translates to `IS NULL` (not `= NULL`), so SQL and C# agree
here. **`TC-AVAIL-EQUIV` gains a row per term**, including a recurring order with a NULL
`CurrentStatus`, so the pairing is pinned rather than assumed.

---

## D4 — Which of the ~~eight~~ **ten** surfaces is wrong, and what each becomes

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
| **9** | `orders.models.ts:169-176` (web row action) | **WRONG — [added by CH-X5]** — a *fourth* web status literal, carrying dead `Pending`; D4 row 5 named only the facade | `[OrderStatus.New, OrderStatus.Confirmed]`, seat term unchanged. **Same edit as row 5, different file** — which is the point: one query literal and one button literal disagreed, and the ADR only saw the query. |
| **10** | `order-details.helpers.ts:108-115` `canTakeOrder` | **WRONG, and it contradicts this ruling — [added by CH-X5]** — `{Pending, Confirmed}` ≡ `{Confirmed}`, so it **hides** Take for a `New` cash order | `[OrderStatus.New, OrderStatus.Confirmed]` + the not-already-assigned term. **This is the highest-value single line in the client work**: without it the cash pipeline the ADR is built to unblock stays unclickable on the web detail page. |
| *11* | `orders.helpers.ts:46-57` `buildOrderStatusOptions` | **WRONG — [added by CH-X6]** — offers dead `Pending`, omits `New` | drop `Pending`, add `New`. Three lines, in a file already being edited for the same reason. In scope: leaving it means the ADR ships a canonical state (`New` + Cash) that the cleaner's own filter cannot name, and a dropdown option that is guaranteed to return zero rows. |

**Two clients out of three were already right, and the majority set was wrong.** Had this been decided
by majority (`{New, Pending, Confirmed}`, 2 of 6 surfaces, plus the dead `Pending` in 3 of 6), the
result would have shipped a dead status to production and left Fact 3 open. Recorded because T-0530
explicitly asked not to pick the majority.

> **Amendment note, 2026-08-03 — rows 6 and 7 are right about STATUS and wrong about SEATS.** This
> table's verdicts are about the **status literal** only, and `{New, Confirmed}` on Android and iOS is
> still correct after **§D9**. What §D9 changes on those two clients is a **different parameter on the
> same call**: `isUnassigned: true` → `hasAvailableSpots: true` **+ `excludeEmployeeId: <own id>`**.
> **Both clients therefore DO change** — do not read "RIGHT / Unchanged" above as "no mobile work".



**The clients do not import the rule** — they cannot evaluate the money conjunct (they filter on
neither `PaymentType`, `PaymentStatus` nor `RecurringTemplateId`, and after the panel's amendment the
rule needs all three), and they do not need to: after #4 the server will not return a non-offerable
row to a browsing cleaner regardless of the client's list. The client lists — **query literals *and*
button gates, rows 5/6/7/9/10** — are kept aligned to the coarse floor by the parity check in D7, not
by trust.

### D4.1 — The date floor is part of the same rule, and the draft left it forked — [added by CH-X7]

The draft aligned the **status/payment** term across the count and the list and left the **date** term
unaligned. Verified:

| Surface | Date floor today |
|---|---|
| Dashboard count + preview (`DashboardSpecifications.CreateAvailableOrdersSpec:18`) | **none** — `cleaningDateFrom: null` |
| Available list, server default (`GetPagedOrders.cs:57-61`) | `now − 2h`, applied **only when `HasAvailableSpots == true`** |
| Available list, web client (`orders.facade.ts:149`) | `cleaningDateFrom ?? new Date()` — **stricter still: `now`** |

Today mobile's count and list agree on the date axis *by accident* — neither has a floor, because
mobile sends `isUnassigned` and so never trips `GetPagedOrders`' default. **§D9's client switch breaks
that accident**: the moment mobile sends `hasAvailableSpots: true` the `-2h` floor fires on the
**list** while the dashboard hero above it still has none. The ADR's stated goal is to stop a partner
seeing "0 available jobs" beside a list of jobs; shipping it as drafted converts that into "**N**
available jobs beside a list of **N−k**" on two clients that agree today. A smaller wrong, newly
introduced by this change.

> **Ruling: there is ONE offerability date floor, it is a named constant, and every surface reads it —
> the same property-not-formula rule §D9.4 applies to the seat cap.** Concretely:
> 1. The `-2h` literal leaves the `GetPagedOrders` handler and becomes a named policy number
>    (`BookingPolicy.OfferableGraceHours = 2`, beside `SpareSeatsPerOrder` — an unexplained literal in
>    an availability rule is a decision nobody made).
> 2. `DashboardSpecifications.CreateAvailableOrdersSpec` applies it, so the count and the preview
>    match the list by construction rather than by review.
> 3. **Web stops sending its own floor** (`orders.facade.ts:149` drops the `?? new Date()`), so all
>    three clients inherit one server-side answer. This is a deliberate, small behaviour change on
>    partner web: jobs that started up to two hours ago and still have a seat become visible there, as
>    they already are on mobile. **Flip condition if that is unwanted:** change the constant — once,
>    for every surface — not one client.
>
> **Rejected alternative** (the challenger offered it as acceptable): *state in writing that the count
> is deliberately floor-free.* No. A count a cleaner cannot reconcile with the list beneath it is the
> exact defect T-0530 exists to close, and "documented divergence" is the weakest of D7's layers —
> a comment.

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
2. **[REPLACED by CH-M5 — the draft action introduced a regression, and its safety argument was a
   sentence telling the implementer to think about it.]** The draft said: *"Remove it from
   `AdminOverrideOrderStatus.cs:56-64` so no new writer can appear. The array is a forward-only ordered
   walk and `Pending` sits between `New` and `Confirmed`, so removing it leaves every other transition
   forward — the implementer confirms the index semantics before landing."* **Verified: it does not.**

   ```csharp
   AdminOverrideOrderStatus.cs:96   var currentRank = Array.IndexOf(Lifecycle, currentStatus ?? OrderStatus.New);
   AdminOverrideOrderStatus.cs:97   var targetRank  = Array.IndexOf(Lifecycle, command.TargetStatus);
   AdminOverrideOrderStatus.cs:101  if (targetRank < 0 || targetRank <= currentRank) -> InvalidOrderStatusTransition
   ```

   With `Pending` gone from `Lifecycle`, a row whose `CurrentStatus == Pending` yields
   `currentRank = -1`; every legal target then satisfies `targetRank >= 0 > -1` and the guard **passes
   for all of them, including `New` at index 0**. The forward-only invariant the array exists to
   enforce is **silently inverted for exactly the rows this change is about** — and D5's own reason for
   "dead, not deleted" is that those legacy rows exist. A legacy `Pending` cash order could then be
   walked **backwards** to `New`, where D1 makes it offerable and takeable.

   **Root cause: one array is serving two different questions** — *what rank is this status* (needs
   every status that can be current, `Pending` included) and *what may an admin target* (must exclude
   `Pending`). Collapsing two questions into one artifact is the precise disease this ADR exists to
   cure; the draft's action was that disease applied to the cure.

   **The corrected action — separate the two:**
   - **`Lifecycle` keeps `Pending`.** It is the *rank* array and must remain total over every status a
     row can currently hold, or ranking breaks for legacy rows.
   - **A new, explicit target guard** rejects `OrderStatus.Pending` as a `TargetStatus` with
     `BusinessErrorMessage.InvalidOrderStatusTransition` — an `OverridableTargets` set, or a named
     guard beside the terminal-state checks at `:83-94`. **This is what stops a new writer appearing**,
     which was the draft's actual goal.
   - **Additionally**, guard the off-lifecycle case on its own merits: `currentRank < 0` must refuse,
     not fall through. It is unreachable once `Lifecycle` stays total, and that is exactly why it
     should be written down — the next member added to `OrderStatus` and forgotten in `Lifecycle`
     re-opens the backwards move otherwise.
   - **Pinned by a test**, seeded from a `Pending` row, asserting `Pending → New` is refused and
     `Pending → Confirmed` still succeeds. Not a code-reading instruction.

   **Knock-on the draft left the implementer to guess (CH-M5):** `TakeOrder.cs:192`'s
   `currentStatus is OrderStatus.New or OrderStatus.Pending` arm becomes **unreachable in its `Pending`
   half** once the take gate ships, because `Pending` is not offerable. **Delete the `or
   OrderStatus.Pending`.** This is not in tension with "readers keep tolerating `Pending`" — that rule
   covers *conservative-direction* readers (`SlotBlockingStatuses`, `GdprDeletionService.cs:92`), which
   treat a `Pending` row as live. This one is a **status-write trigger**, and a dead branch that writes
   status is a trap, not tolerance.
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

> **⚠️ [SUPERSEDED WITHIN THIS ADR by §D6.1–D6.4.]** The bill below is the draft's and is **wrong in
> its exemplar, its namespace and its stated failure mode**, and it budgets copy for the rare key and
> none for the modal one. **Read §D6.3 for the bill you implement.** It is left in place because the
> panel's corrections are only legible beside what they correct.

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
| `NoAvailableSpots` | Lies **for the residue**. The seat exists; the order is dead. *(But see D6.2 — at seat cap 1 this key is the modal refusal for a different reason, and its copy is wrong too.)* |
| `OrderNotFound` | **Reserved by ADR-0036** for the *hold* refusal specifically so exclusivity cannot be inferred. Overloading it here both dilutes that purpose and tells a cleaner "not found" about an order on their screen. |

### D6.1 — The refusal is a **taxonomy**, not a key — [amended by CH-M1]

The draft's table considered three candidates and missed the two that name the two most likely
refusals, **which already exist and are already localized on both mobile clients**:

```
BusinessErrorMessage.cs:49   OrderAlreadyCancelled = "order.already_cancelled"
BusinessErrorMessage.cs:50   OrderAlreadyCompleted = "order.already_completed"
```

They are not hypothetical: `AdminOverrideOrderStatus.Handler` refuses **exactly these two statuses
with exactly these two keys** (`:83-88`, `:89-94`) — ten lines above the lifecycle walk D5 edits.
**D6's own first and strongest argument for shipping the gate is family consistency** (*"the only
unguarded command in its own family"*). The same argument one level down says `TakeOrder` refuses
`Cancelled`/`Completed` the way its family already does. **An author cannot invoke family consistency
for the gate and decline it for the vocabulary.** Inventing a third name for the same two states
would be a fourth row in the "surfaces that disagree about order status" table, authored by the
document closing that table.

> **Ruling — three keys, not one:**
>
> | Refused because | Key | Cleaner's next move |
> |---|---|---|
> | `Cancelled` | **`order.already_cancelled`** *(exists)* | gone for good — refresh, stop looking |
> | `Completed` | **`order.already_completed`** *(exists)* | someone finished it — refresh |
> | everything else (`New`+Card, `Confirmed`+Card unpaid, `New`+Cash recurring-unconfirmed, `OnTheWay`, `InProgress`, legacy `Pending`) | **`order.not_takeable`** *(new)* | the row is stale — refresh |
>
> **The residue key stays one key, and it stays opaque. The challenger's proposed
> `order.not_yet_payable` for the `New`+Card row is REJECTED on two grounds:**
> 1. **It discloses the customer's payment state to a third party.** A cleaner is not a party to the
>    customer's payment. "Not payable yet" tells them the customer has not paid — a disclosure the
>    take refusal has no business making, and one no other partner-facing error makes.
> 2. **It is false for most of the residue and misleading for the rest.** "Try again in a minute" is
>    wrong for `OnTheWay`/`InProgress` (work has begun) and wrong for the modal `New`+Card case (an
>    abandoned checkout never becomes payable — it gets cancelled ~1h15m in).
>
> **Bound copy for the residue, all 11 files: "This job is no longer available."** The key names the
> mechanism for the developer; the sentence names the outcome for the cleaner; neither leaks the
> customer's money.

### D6.2 — The seat ruling made the **other** key the common one — [amended by CH-X2]

Trace the ordering D6 mandates against the shipped seat cap. A job another cleaner took two seconds
ago is `Confirmed` (`TakeOrder.cs:192-194`), so `IsOfferable` **passes** and the refusal falls through
to `HasAvailableSpots` → `BusinessErrorMessage.NoAvailableSpots` (`TakeOrder.cs:44-45`). **The race
never produces `order.not_takeable`.** It produces the key the table above dismisses as *"Lies. The
seat exists"* — which after `SpareSeatsPerOrder = 0` is itself wrong: on the modal booking the seat
does **not** exist, and this is the only refusal most cleaners will ever see.

And the frequency is manufactured, not incidental: `NewJobsDigestService.cs:62-74` selects **every**
Approved/Active cleaner with a matching `WorkCountryId` — no radius, no cap, no shortlist, no
staggering (ADR-0036's hold is a *preferred-cleaner* perk, not a race breaker for the open board). One
single-seat job pushed to 20 cleaners yields **1 winner and 19 identical "no available spots" toasts**,
a sentence that names a capacity concept rather than the thing that happened.

> **Ruling: `order.no_available_spots` is re-voiced in the same change, across all 11 partner files —
> "Another cleaner has already taken this job."** It is a **reword of an existing key, not a new one**
> (no new key, no new contract, no NSwag impact), in files the ticket has open anyway. Two reasons it
> is in scope rather than filed:
> - **The ADR's copy work is otherwise aimed at the rare key and none at the common one.** Shipping
>   the gate without this means the panel knowingly left the modal partner experience wrong.
> - **It is the same edit that fixes CH-X1** (below) on iOS — one string, two defects.
>
> **Admin copy is NOT re-voiced.** `AdminReassignOrder.cs:95` emits the same key to a different
> audience with a different truth (an admin adding a cleaner to a full order really is out of seats).
> Admin web has its own locale files, so the two voices do not collide — **and that separation is the
> per-audience seam working**, which is exactly what D6.3 rules must be preserved.

### D6.3 — The copy bill, restated — [amended by CH-M6 + CH-X1 + CH-X4]

The draft's bill was *"15 strings across 11 files… traced against the existing
`order.weekly_limit_reached` key"*. **The exemplar it was traced against is missing from 5 of those 11
files.** Verified: `weekly_limit_reached` is absent from **every** partner-web locale
(`apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` — the only `api.order.*` key near it
is `no_available_spots` at `:1075`), while Android, iOS and **customer** web all have it. It is thrown
by `TakeOrder.cs:57-58` — this very validator. **So today a cleaner on partner web who hits their
3/6/10 weekly cap is shown "An error occurred. Please try again." and retries forever.**

**Three mechanical corrections the bill needs, or a developer following the project guide gets it
wrong:**

1. **The namespace is `api.*` on web, not `errors.*`.** `http-error.interceptor.ts:15` builds
   `` `api.${errorKey}` `` — one shared interceptor for all three web apps. The root `CLAUDE.md` tells
   developers *"Every backend error key … must have a corresponding frontend translation under
   `errors.*`"*. **That instruction is wrong and has a live cost**; routed to the docs agent
   (§Escalations). Per client: **`api.order.*`** (web) · **`error_order_*`** (Android `strings.xml`) ·
   **`error.order.*`** (iOS `Localizable.xcstrings`).
2. **A missing key is SILENT on web, not visible.** `http-error.interceptor.ts:14-20` deliberately
   swallows the raw key and substitutes `api.common.error_occurred`. The draft's verification step 6
   (*"a missing one shows the raw key"*) is true on Android (`ApiErrorTranslator.kt:70`) and iOS
   (`ApiErrorLocalizer.swift:18-20`) and **false on web** — which is precisely how
   `weekly_limit_reached` stayed missing. Step 6 is replaced (see verification).
3. **iOS partner has no `error.order.*` table of its own.** `ApiErrorLocalizer.swift:29-33` resolves
   **only** from `CoreL10n.bundle` — the shared `CleansiaCore` catalog — and never probes the app
   bundle. So *every* partner error string the ADR adds lands in a table the customer app also reads.

**The CH-X1 defect, and the correction to its diagnosis.** The challenger is right that a cleaner
losing a race today reads a customer's sentence: `Localizable.xcstrings:3502-3532`
`error.order.no_available_spots` en = *"No cleaners are available for that slot. Please pick another
time."* — untrue (they **are** the cleaner) and unactionable (there is no time for them to pick). But
the diagnosis is **not** a persona collision requiring an architecture change: **no customer-facing
command emits this key.** `rg NoAvailableSpots src --type cs` returns exactly two production emitters —
`TakeOrder.cs:45` (partner) and `AdminReassignOrder.cs:95` (admin).
`BookingSubmitOutcome.swift:7-10` merely *lists it as an example* in a doc comment; the customer
booking sheet cannot receive it from the server.

> **Ruling: fix the string, do not add a lookup path.**
> - The five `error.order.no_available_spots` strings in the shared catalog are **re-voiced for the
>   partner** in this change — which is the same edit D6.2 already requires. No customer surface
>   regresses, because no customer surface can receive the key.
> - **`ApiErrorLocalizer` is NOT changed to probe the app bundle.** That would create a second lookup
>   path and a per-app override seam for **one** mis-authored string — a new seam needing its own
>   parity guard, to solve a problem that does not exist. Rejected on cost, not on principle.
> - **New catalog rule, and this is the durable half of the finding:** *a key in the shared
>   `CleansiaCore` catalog must be voiced correctly for **every** persona that can receive it. If two
>   personas can receive one key and need different sentences, the **backend emits two keys** — the
>   client never branches on audience.* That preserves the per-audience host seam instead of pushing
>   audience-awareness down into a shared localizer. Recorded in `agents/knowledge/patterns-mobile.md`
>   / `consistency.md` alongside this ADR.

**The bill, corrected:**

| Item | Files | Note |
|---|---|---|
| **New** `order.not_takeable` — *"This job is no longer available."* | 11 (web ×5, Android ×5, iOS ×1/5 langs) | as drafted |
| **Reuse** `order.already_cancelled`, `order.already_completed` | Android ✅ `strings.xml:1092,1093` · iOS ✅ `Localizable.xcstrings:2802,2837` · **web ✗ — add both ×5** | 2 keys × 5 web files = 10 strings, **0 on mobile** |
| **Re-voice** `order.no_available_spots` | 11 | reword, not a new key |
| **Backfill** `order.weekly_limit_reached` on partner web | 5 | **a live defect this panel found**, in the file the draft used as its cost model |

**No migration. No NSwag regen.** No DTO or endpoint shape changes; three of the four rows are locale
files only and the fourth is a `const string`.

### D6.4 — Partner **web** cannot recover from a refusal, and the gate adds one — [amended by CH-X3]

Both web take call sites drop the error on the floor. `orders.facade.ts:207-218` subscribes with **no
error callback**, and the two reloads sit **inside** the success branch (it is also the only call in
that facade without `takeUntil(this.destroyed$)` — compare `:223`). `order-details.facade.ts:189-202`
uses `catchError(() => of(null))` with `loadOrderDetails` inside the `tap`, so the detail page never
re-reads either. The refusal *is* announced by the interceptor, but nothing tells the board its row is
stale — and there is no confirm step to absorb the mistake (`orders.component.ts:235-237` fires
straight off the row action). The cleaner can click the same dead job forever, getting an identical
3-second toast with no state change to distinguish "I already tried this" from "I haven't".

Both mobile clients already do this correctly and their own comments name the scenario — Android
`OrdersListViewModel.kt:355-368` (*"A reject nearly always means the order moved on without us …
the row must stop offering an action the server has already refused"*), iOS
`OrdersListViewModel.swift:178-184`. **This is a web-only gap, and the ADR is adding a new refusal
path to the one client that cannot reconcile.**

> **Ruling: the web reconcile is IN SCOPE for the take-gate ticket.** D6's cost argument is *"the gate
> is ~6 lines against a validator being opened anyway"* — true for the backend, and the honest bill
> includes the client that has to survive the new refusal. An error branch on both facades (snackbar
> is already handled by the interceptor; the branch reloads the affected pane) plus the missing
> `takeUntil`. **Shipping a refusal into a UI with no reconcile is not "the implementer will notice" —
> it is a defect with a design decision in front of it.**

**Interaction with ADR-0036 — rule ordering is load-bearing, and [AMENDED by CH-M2] the mechanism the
draft named does not deliver it.** ADR-0036 folds the hold refusal *into the existence rule*
(`preferred-cleaner-dispatch.md:160-163`) so a held order returns `OrderNotFound`. That rule must
therefore be evaluated **before** the status rule, so a held order **never** returns
`order.not_takeable` (which would reveal that the order exists and is live — precisely the inference
ADR-0036 forbids). The required order is:

```
NotEmpty → ExistsAsync (incl. ADR-0036 hold) → IsOfferable → HasAvailableSpots
```

`IsOfferable` goes **before** `HasAvailableSpots`: for a `Cancelled` order with a free seat the honest
answer is "this job is no longer available", not "no spots".

> #### The ordering guarantee does not exist today, and the draft declared it load-bearing
>
> The draft wrote *"Under `Cascade.Stop` on `RuleFor(x => x.OrderId)`, the required order is…"*.
> **Verified false, three links deep:**
>
> 1. **`Cascade.Stop` is rule-level, and `TakeOrder.Validator` has TWO chains** —
>    `RuleFor(x => x.OrderId)` at `:38-45` and `RuleFor(x => x)` at `:47-60`. FluentValidation
>    **12.1.1** (`src/Directory.Packages.props:26`) defaults `ClassLevelCascadeMode` to `Continue`, and
>    nothing in this repo sets it. **Both chains always run.** Placing `IsOfferable` "after
>    `ExistsAsync`" orders it against three rules and leaves it unordered against six.
> 2. **The pipeline returns every failure**, not the first —
>    `ValidationPipelineBehavior.cs:38-48` maps `validationResult.Errors` to `Error(failure.ErrorCode,
>    failure.ErrorMessage)`.
> 3. **The transport collapses them into an unresolvable composite** —
>    `CleansiaApiController.cs:93-99` groups by `Error.Code` and joins with `"; "`. Every `MustAsync`
>    rule in this validator carries FluentValidation's *same* default `ErrorCode`
>    (`AsyncPredicateValidator`), **not** the property name, so two async failures produce **one**
>    dictionary entry valued `"order.not_found; order.time_conflict"`. Web resolves that against
>    nothing and shows the generic message (`http-error.interceptor.ts:14-20,45-48`); Android shows the
>    **raw joined string** (`ApiErrorTranslator.kt:70`, `lookupKey(key) ?: key`).
>
> **It fires today.** `NotHaveTimeConflictAsync` returns `false` when the order is missing
> (`TakeOrder.cs:150-154`), so an unknown order id already yields `order.not_found;
> order.time_conflict` in production.
>
> **And that is worse than a wrong sequence — it inverts ADR-0036's protection.** Chain 2 queries the
> real order regardless of chain 1's verdict, so:
>
> | Scenario | Chain 1 | Chain 2 (runs anyway) | Response |
> |---|---|---|---|
> | id does not exist | `order.not_found` | `order.time_conflict` (`order == null → false`, `:154`) | `order.not_found; order.time_conflict` |
> | order exists but is **held** (ADR-0036), no overlap | `order.not_found` | passes | **`order.not_found` alone** |
>
> **A bare `order.not_found` proves the order exists and does not overlap the caller's calendar.**
> Existence is inferable from the *pairing*, which defeats exactly what ADR-0036 folded the hold into
> the existence rule to prevent. ADR-0037 does not create this — but it **asserts the property holds**
> and then leans on that assertion to justify adding a fourth key to the same response, which strictly
> increases the number of multi-error responses. That is not admissible.
>
> #### Ruling — the shape is structural, the enforcement is a test, and neither is a comment
>
> **(a) `TakeOrder.Validator` collapses to ONE ordered chain.** A single
> `RuleFor(x => x).Cascade(CascadeMode.Stop)` carrying every rule in the required order:
>
> ```
> OrderId not empty → ExistsAsync (incl. the ADR-0036 hold) → IsOfferable → HasAvailableSpots
>   → caller-is-employee → profile → approval → already-assigned → weekly cap → time conflict
> ```
>
> **Why one chain and not `ClassLevelCascadeMode = CascadeMode.Stop`.** Both produce exactly one error.
> The class-level option is one line — and it is *action at a distance*: a property set in a
> constructor, invisible at every rule site, silently re-openable by anyone who adds a third `RuleFor`
> or changes a global. **One chain makes the ordering the draft calls load-bearing readable in the one
> place it must be read**, which is D7 layer 1 (*structural — delete the duplication; the only layer
> that cannot rot*) applied to this validator. Accepted equivalent **only** with (b) also in place;
> preferred form is the single chain.
> - *Cost, stated so it is not discovered:* the failures move from property `OrderId` to the
>   whole-object rule. **No client is affected** — every client keys on the `errors` dictionary value
>   (the business message), and the dictionary is grouped by `ErrorCode`, never by property name
>   (`CleansiaApiController.cs:93-99`).
> - *`TakeOrder.cs:154`'s `return false` for a null order becomes unreachable* (the chain stops at
>   `ExistsAsync`). Leave the defensive line; the test in (b) pins that it can no longer surface.
>
> **(b) `TC-TAKE-ONE-ERROR` — the enforcement.** A test asserting `result.Errors` contains **exactly
> one** entry, per refusal scenario: unknown id, **held order (ADR-0036)**, not-offerable, no seat,
> already-assigned, weekly cap, time conflict. It goes red the moment a second chain reappears or a
> rule is reordered. **Verification step 5 stops being "confirm `IsOfferable` sits after `ExistsAsync`"
> — a code-reading instruction, i.e. precisely the comment-as-enforcement D7 forbids — and becomes this
> test.** The draft already demanded ordering be pinned by a test; it pointed the demand at the wrong
> thing.
>
> **(c) One change, two fixes.** The multi-error composite is a **live defect** — an unknown order id
> resolves to nothing on web and shows a raw joined machine string on Android *today*. The single chain
> closes it, and closes the ADR-0036 inference leak, as a by-product of making this ADR's own ordering
> claim true. **This is in scope for the take-gate ticket**, not a follow-up: the gate cannot be
> defended without it.

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

**2. Cross-stack parity check — the only layer that spans the drift.** The drift lives across C#,
TypeScript, Kotlin and Swift; no compiler and no single-stack linter spans it. The *technique* is
proven: `error-contract-parity.spec.ts:43-52` already parses **C# source** and locates the solution
root by walking up to `Cleansia.Api.sln` (`:9-20`).

> **[AMENDED by CH-M7 — the draft's delivery vehicle does not run on the edits it exists to catch. An
> unenforced enforcement mechanism is WORSE than none, because the ADR then claims coverage it lacks.
> This is the finding the panel took most seriously, because D7's own thesis is "a comment is not
> enforcement" and the draft's answer was a test with no trigger.]**
>
> **What was verified about the drafted shape (a Jest spec run by `nx affected`):**
>
> | Leg | Evidence |
> |---|---|
> | A mobile-only or Domain-only diff selects **zero** Nx projects, so the spec is **not run** | `frontend-ci.yml:86` — `npx nx affected -t test --base="$NX_BASE"`. The PR trigger has no `paths` filter, so the *job* starts; the *spec* does not. |
> | On `master` the workflow does not even start for those trees | `frontend-ci.yml:12-17` — push paths are `src/Cleansia.App/**` + the workflow file. |
> | Worse than not-selected: **Nx serves a cached PASS if it *is* selected** | `nx.json` — `@nx/jest:jest` inputs are `["default", "^production", "{workspaceRoot}/jest.preset.js"]`, `default` = `["{projectRoot}/**/*", "sharedGlobals"]`, `sharedGlobals` = `[]`. `{workspaceRoot}` is `src/Cleansia.App`, and Nx inputs cannot reference paths above it — so `OrderAvailability.cs`, `OrdersListViewModel.kt` and `OrdersListLogic.swift` **are not declared inputs**. Change a Kotlin literal, touch one unrelated TS file, and the guard replays green while the thing it guards has drifted. |
> | Relocating to backend CI does not help | `backend-ci.yml:14-19,23-28` explicitly **excludes** `src/cleansia_android/**` and `src/cleansia_ios/**`. `android-ci`/`ios-ci` are Gradle/Xcode and cannot read C#. |
>
> **There is no workflow in this repo that fires on a mobile-only change and can read C# source.** As
> drafted, layer 2 would have caught **none** of the three mobile drifts D0 lists.
>
> #### Ruling — a plain script with its own trigger, not a Jest spec inside Nx
>
> 1. **It is not a Jest spec and does not live in the Nx workspace.** It is a **plain Node script**
>    (`check-available-status-parity.mjs`), same species as `tools/typecheck-apps.mjs`. This removes
>    the cache hazard **by construction** rather than by configuration — the structural fix, which is
>    layer 1's own logic applied to layer 2. A cache-key setting can be regressed by an unrelated
>    `nx.json` edit; not being cacheable cannot.
> 2. **It runs unconditionally**, never behind `nx affected`. The precedent is eleven lines above the
>    broken one in the same file: `frontend-ci.yml:79-81`, *"Regen-drift guard self-test"* — an
>    unconditional non-Nx step, added for exactly this reason.
> 3. **Its trigger covers all four trees it reads**: `src/Cleansia.Core.Domain/**`,
>    `src/Cleansia.App/**`, `src/cleansia_android/**`, `src/cleansia_ios/**`, on `pull_request` **and**
>    `push: master`. **Preferred form: its own repo-root workflow**, so the check is not coupled to
>    `frontend-ci`'s deliberately narrow paths scope (widening that workflow would spin three Angular
>    production builds on every Swift commit — a real cost, and the reason it is scoped). Accepted
>    alternative: an unconditional step in `frontend-ci` **plus** the widened push paths — both, never
>    one.
> 4. **It covers BUTTON gates, not only query literals** — surfaces 5, 6, 7, **9** and **10**
>    (`orders.facade.ts`, `OrdersListViewModel.kt`, `OrdersListLogic.swift`, `orders.models.ts`,
>    `order-details.helpers.ts`). CH-X5's sharpest point: a parity check that covers the query and not
>    the button tests the wrong half — the query decides what is *listed*, the button decides what is
>    *clickable*, and this ADR's whole thesis is that those must not diverge. Surface 10 is live proof:
>    the drafted three-file spec would have gone **green** while the button hid the cash pipeline.
> 5. **Its acceptance test is behavioural, and the reviewer runs it:** *delete one status from one
>    client literal, push a branch touching only that file, and the PR must go red.* If it does not,
>    layer 2 does not exist. **If (1)–(4) are not delivered, this ADR requires that D7 layer 2 be
>    written down as ADVISORY** — the one thing that may not happen is the ADR claiming an enforcement
>    it does not have.

**3. `check-consistency.mjs` — cheap mechanical backstop** (`agents/knowledge/consistency.md`, backend
section): flag any `OrderStatus[]` literal outside `OrderAvailability.cs` that contains
`OrderStatus.Pending`, or that looks like an available/offerable set. Heuristic and line-based —
necessary, not sufficient, per that tool's own preamble (`:16-18`).

> **[CLARIFIED — the challenger noted `check-consistency.mjs` has no CI wiring. True, and it is the
> documented design, not a gap:** `process/enforcement.md:16,83` places it at **T2-ADVISORY, run by the
> Reviewer** per ticket, with CI promotion gated on a stack's baseline reaching zero (`:95`). So the
> observation does not blunt layer 3's stated role. **But it does mean layer 3 cannot substitute for
> layer 2**, and there is a second reason it cannot: `check-consistency.mjs`'s walker globs
> `.cs`/`.ts`/`.kt` **only — no Swift at all** (`enforcement.md:17`). It is structurally incapable of
> seeing the iOS literal. That is the strongest single argument for ruling (1)–(4) above.]

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
Without it, a mobile cleaner's Available tab would list jobs they are **already on**, and tapping one
returns `TakeOrder.cs:55-56`'s already-assigned refusal.

> **[CORRECTED by CH-M8c — the requirement survives; its stated reason does not.]** The draft justified
> this with *"— **every one of which carries a spare seat** —"*. After the owner's `Q-AVAIL-03` ruling
> (`SpareSeatsPerOrder = 0`) that is false: a 1-seat order you are on is **full**, so
> `hasAvailableSpots: true` already filters it out. **The regression is now confined to
> `RequiredEmployees ≥ 2` orders that are not yet fully crewed** — smaller, still real, and still a
> tab listing jobs that error on tap. `excludeEmployeeId` remains **required** and verification step 10's
> hard-reject remains right. Restated because a reviewer who checks the *stated* reason will find it
> false and may wave the diff through — which is exactly how a correct rule dies.

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

~~**Interim, so nothing is blocked: `MaxEmployees` stands, unchanged.**~~ **[SUPERSEDED IN FACT by
CH-M8d — the interim expired before the panel convened.]** The owner answered `Q-AVAIL-03` and it
**shipped**: `BookingPolicy.cs:76` `SpareSeatsPerOrder = 0`, consumed at `OrderFactory.cs:148` →
`Order.CalculateRequiredEmployees(spareSeats)` (`Order.cs:534`), pinned by `OrderSeatCapacityTests.cs`.
**`MaxEmployees` now always equals `RequiredEmployees`.** Do not read the struck sentence as current
state.

**And the flip proved the rule.** D9.4's property-not-formula ruling was written before the number was
known, precisely so the answer would cost one line — and it did: the arithmetic lives only in
`Order.CalculateRequiredEmployees`, and `OrderSpecification.cs:126,138`, `Order.AvailableSpots`,
`NewJobsDigestService.cs:101`, `OrderMappers.cs:101` and `OrderAccessService.cs:85` all read the
property and needed no edit. **That is the one part of this ADR that has already been tested against
reality, and it held.**

### D9.5 — What D9 does **not** decide

- **It does not change the take gate.** `TakeOrder`'s seat check is `HasAvailableSpots`
  (`TakeOrder.cs:44`) and stays exactly as it is — offer and take keep the **same** seat term, per D2.
- ~~**It does not introduce a "covered but not full" state.** `IsFullyAssigned` names it and is
  unused…~~ **[VACATED by CH-M8d.]** `Q-AVAIL-03` came back as *"seats = `RequiredEmployees`"*, so
  **covered ⟺ full**: `HasAvailableSpots` and `IsFullyAssigned` denoted the same predicate and
  `IsFullyAssigned` has been **deleted** (`rg IsFullyAssigned src/` → zero hits — re-verified by the
  panel). There is no covered-but-not-full state and no vocabulary needed for one. The paragraph is
  vacated rather than deleted so a reader of the original escalation can see the branch that closed.
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

**Added by the panel — the alternatives the draft's why-not rows answered a different question about.
A4/A5 are correct *as written* and are re-argued here against the propositions actually on the table:**

| # | Alternative | Why not |
|---|---|---|
| A15 | **`PaymentStatus` in *conjunction* with the status term** — i.e. what ships | **ADOPTED (CH-M4).** A4/A5 reject `PaymentStatus` as a **whole rule** and are right; neither addressed the conjunction, which is a different proposition. A5's "excludes every cash order ever" does **not** apply once the `Cash ∧ non-recurring` disjunct carries them. Equivalent to the draft on every normal path (the webhook writes money + status in one commit) and strictly safer on the admin-override and card-decline paths. |
| A16 | **Keep the draft rule; require a payment guard on `AdminOverrideOrderStatus` instead** | Fixes one of the two reachable paths and not the other — a **declined** card is deliberately left `PaymentStatus.Pending` for retry (`HandlePaymentNotification.cs:230-242`), with no override involved. And it puts the availability rule's correctness in a *different aggregate's* validator, where a future writer can break it without touching `OrderAvailability`. The guard is still worth adding; it is not the fix. |
| A17 | **Keep `New`+Cash recurring offerable and fix `AutoCancelStaleRecurringOrders` instead** (add a `PaymentType` term) | **The sweep is correct and must not be weakened.** On a recurring order `PaymentStatus == Pending` *is* "the customer has not confirmed this occurrence" — `ConfirmRecurringOrder.cs:111-112` writes `Paid` for **cash** too. Adding a `PaymentType` term would leave unconfirmed cash occurrences live and a cleaner standing on a booking the customer never accepted. The offer was wrong, not the sweep. |
| A18 | **Escalate `New`+Cash+recurring to the owner** as a product call | Rejected as an escalation: there is no trade-off to price. Every option that keeps them offerable ends with a cleaner losing a booked slot **one hour before it starts** — strictly worse than the card case the ADR already refuses on the same reasoning. This is the architecture reading its own invariant correctly, not a business preference. |
| A19 | **`ClassLevelCascadeMode = CascadeMode.Stop`** on `TakeOrder.Validator` (CH-M2's suggestion) | Produces the right result and is one line — accepted **only** alongside `TC-TAKE-ONE-ERROR`. Not preferred: it is action at a distance, invisible at every rule site, and silently re-openable by a third `RuleFor`. **One ordered chain makes the load-bearing property readable where it must be read.** See D6. |
| A20 | **Make `ApiErrorLocalizer` probe the partner app bundle before `CoreL10n`** (CH-X1's option (a)) | A second lookup path and a per-app override seam — needing its own parity guard — to solve **one** mis-authored string. No customer-facing command emits `order.no_available_spots` (`rg NoAvailableSpots src --type cs` → `TakeOrder.cs:45`, `AdminReassignOrder.cs:95`), so re-voicing the shared string is correct and complete. Rejected on cost. The **rule** that replaces it (D6.3) is where the durable value is. |
| A21 | **Document the dashboard count as deliberately floor-free** (CH-X7's option (b)) | "Documented divergence" is D7's weakest layer wearing a different hat. A count a cleaner cannot reconcile with the list beneath it is the defect T-0530 exists to close; §D9 would *newly introduce* it on two clients that agree today. One named constant, read by both. See D4.1. |
| A22 | **One opaque `order.not_takeable` for every refusal** (the draft) | Invents a third vocabulary for two states the same partner-facing family already refuses with `order.already_cancelled` / `order.already_completed` (`AdminOverrideOrderStatus.cs:83-94`), both already localized on Android and iOS. D6's own family-consistency argument, applied one level down, forbids it. See D6.1. |
| A23 | **`order.not_yet_payable` for the `New`+Card row** (CH-M1's suggestion) | Discloses the **customer's** payment state to a cleaner who is not a party to it, and is false for the rest of the residue (`OnTheWay`/`InProgress`) and misleading for the modal case (an abandoned checkout never becomes payable — it gets cancelled). One opaque residue key with bound copy. |

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
5. **[REPLACED by CH-M2 — the drafted step was a code-reading instruction, i.e. the
   comment-as-enforcement D7 forbids.]** `TakeOrder.Validator` is **ONE** `RuleFor(x => x)` chain with
   `Cascade(CascadeMode.Stop)` (`rg -c "RuleFor" TakeOrder.cs` → **1**), in the order `not-empty →
   ExistsAsync → IsOfferable → HasAvailableSpots → …cleaner rules`. **The check is
   `TC-TAKE-ONE-ERROR`**: every refusal scenario returns **exactly one** error, including the ADR-0036
   **held** order. A second `RuleFor` in this file is a hard reject.
6. **[REPLACED by CH-M6 + CH-X4 — the drafted step ("a missing one shows the raw key") is FALSE on web
   and is how `order.weekly_limit_reached` stayed missing.]** Do not look at a screen. **Grep the
   files**, per client namespace:
   - `rg -c '"not_takeable"' src/Cleansia.App/apps/cleansia-partner.app/src/assets/i18n/*.json` → **5**
   - `rg -c 'error_order_not_takeable' src/cleansia_android/partner-app/src/main/res/values*/strings.xml` → **5**
   - `rg -c 'error.order.not_takeable' src/cleansia_ios/CleansiaCore/…/Localizable.xcstrings` → **1 key, 5 languages**
   - …and the same three greps for `already_cancelled`, `already_completed`, `no_available_spots`
     (re-voiced) and `weekly_limit_reached` (backfilled on web).
   A missing web key renders `api.common.error_occurred` — **indistinguishable from a 500**
   (`http-error.interceptor.ts:14-20`).
7. **[WIDENED by CH-M7 + CH-X5.]** The parity check is a **plain Node script with its own trigger**,
   not an `nx affected` Jest spec, and it covers **query literals AND button gates** (surfaces
   5/6/7/**9**/**10**). Acceptance: *delete one status from one client literal, push a branch touching
   only that file, and the PR goes red.* If it stays green, layer 2 does not exist and the ADR requires
   it be labelled ADVISORY.
8. **[WIDENED by CH-M3 + CH-M4 + CH-X8.]** AC4's tests run on one fixture and agree across the digest,
   the board, the dashboard count and the take gate for **six** rows, not two:
   `New`+Cash one-off (offered / counted / takeable) · `New`+Card (not) · `New`+Cash **recurring,
   unconfirmed** (not) · `Confirmed`+Cash recurring **`Paid`** (offered) · `Confirmed`+Card
   **`PaymentStatus.Pending`** (not) · and **the sweep case**: run `CleanupStalePendingOrders` over the
   fixture and assert the card order **leaves** the offerable set. That last one pins the ADR's central
   premise, which the draft asserted and never tested.
   *(The late-payment resurrection — `Cancelled → Confirmed` via `HandlePaymentNotification.cs:254` —
   is **out of scope for AC4 in writing**: it exercises Stripe webhook handling, and the predicate is
   stateless so it re-evaluates correctly by construction. Filed separately; see §Escalations.)*
8a. **[added by CH-M5]** `AdminOverrideOrderStatus`: `Lifecycle` **still contains `Pending`** (it is the
   rank array), and a **separate** guard refuses `Pending` as a `TargetStatus`. Seeded test: a
   `Pending` row cannot be walked **backwards** to `New`. A diff that deletes `Pending` from
   `Lifecycle` is a hard reject — `Array.IndexOf` returns `-1` and the forward-only guard inverts.
8b. **[added by CH-X3]** Both web take call sites (`orders.facade.ts:207-218`,
   `order-details.facade.ts:189-202`) have an **error branch that reloads the affected pane**, and the
   board call has `takeUntil(this.destroyed$)`. A refused take must not leave a clickable dead row.
8c. **[added by CH-M9]** The `PaymentType` exhaustiveness test exists and goes red on a new enum member
   until `OrderAvailability` classifies it on both axes.

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
- **`Q-AVAIL-03` — the seat cap. ANSWERED 2026-08-03 by the owner: _"Seats = RequiredEmployees."_**
  No spare seat. *(Original question, preserved: should an order carry seats for exactly the crew the
  work needs (`RequiredEmployees = ceil(EstimatedTime / 120)`), or one spare (`RequiredEmployees + 1`,
  the then-shipped value with no recorded rationale)? Each filled seat beyond the required crew costs
  **a second full labour payment** — `CalculateOrderPay:140-152` writes one pay row per assigned
  employee and `CalculateAggregatedPay:30-61` has no crew-size term, so on the modal single-cleaner
  booking a filled spare seat doubles labour cost at an unchanged customer price.)*

  **Consequences of the ruling, binding on the implementation:**
  - `MaxEmployees = RequiredEmployees + BookingPolicy.SpareSeatsPerOrder`, with the constant at **0**.
    Expressed as a named constant rather than dropping the term, so the record shows a spare seat was
    considered and deliberately set to zero, and so a future change stays the one-line flip §D9.4's
    property-not-formula rule was written to preserve.
  - **`HasAvailableSpots` and `IsFullyAssigned` now denote the same predicate** (`MaxEmployees −
    assigned > 0` vs `assigned >= RequiredEmployees`). Two independently-maintained expressions of one
    rule is the precise defect class this sprint has spent its time closing — one must delegate to the
    other or be deleted. `IsFullyAssigned` (`Order.cs:118`) is read by nothing today.
  - **`MaxEmployees` is not removed.** It would now always equal `RequiredEmployees`, but it is a wire
    field on generated clients and dropping it would force an owner-only NSwag regen for no behavioural
    gain.
  - The owner's phrase *"based on the calculations of how much work there is"* (the same message that
    answered `Q-AVAIL-01`) maps exactly onto `RequiredEmployees`, which **is** that calculation. The
    `+ 1` never was — which is why it is struck rather than tuned.
  - This does **not** reopen `Q-AVAIL-01`: a second cleaner may still join a partly-crewed booking. It
    bounds how many seats exist to fill, not who may fill them.
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

### Found by the defense panel, 2026-08-03 — **defects to file, not decisions** (PM: these are not in T-0530)

Listed here because this ADR promoted `CleanupStalePendingOrders` to *"the sweep that actually runs"*
and now owns that premise; leaving the next reader to re-derive these is the disease this document
exists to treat.

1. **SEVERE — every recurring CARD order appears to die ~1h after materialization, before the customer
   can ever confirm it** (surfaced by CH-M4, chain re-verified). `MaterializeRecurringBookings` creates
   the occurrence at 02:00 UTC with `PaymentType: template.PaymentType` (`:131`) and
   `PaymentStatus.Pending` (`OrderFactory.cs:116`), for a slot up to 7 days out (`:27`).
   `CleanupStalePendingOrders` cancels **anything** matching `PaymentStatus == Pending ∧ PaymentType ==
   Card ∧ CreatedOn < now−1h` (`:50-53`) — **no `OrderStatus` term and no `RecurringTemplateId`
   exclusion** — on a 15-minute timer. So the order is cancelled around 03:15, before the reminder push
   and before `ConfirmRecurringOrder` can be called. **Recurring card bookings would be structurally
   impossible.** This does **not** falsify ADR-0037 (such orders are `New` + Card → not offerable
   either way), which is why it is a filed defect and not a blocking challenge. **Verify against DEV
   data before sizing** — if confirmed, it is the highest-priority item this panel produced.
2. **`CleanupStalePendingOrders` cancels SILENTLY** (CH-X8a) — `:69-79` writes
   `UpdatePaymentStatus(Failed)` + `AddOrderStatus(Cancelled)` and **dispatches nothing**. It is the
   only production writer of `OrderStatus.Cancelled` that does not emit
   `NotificationEventCatalog.OrderCancelled`; both siblings do (`HandlePaymentNotification.cs:306-319`,
   `AutoCancelStaleRecurringOrders.cs:86-98`). A customer's booking vanishes with no message.
3. **`order.weekly_limit_reached` is missing from all five partner-web locales** (CH-M6/CH-X4) —
   thrown by `TakeOrder.cs:57-58`, so a cleaner at their 3/6/10 cap sees "An error occurred. Please try
   again." **Fixed inside D6.3's bill** (listed here so it is visible as a pre-existing defect, not a
   new cost of this ADR).
4. **The root `CLAUDE.md` names the wrong i18n namespace** (CH-M6) — it says backend error keys map to
   `errors.*`; web resolves `api.*` (`http-error.interceptor.ts:15`, one shared interceptor for all
   three apps). A developer following the project guide puts the key where it never resolves, and the
   web fallback hides the mistake. **Docs agent**, same ticket as the lifecycle correction.
5. **The web order-detail status timeline is off by one** (CH-X9) — `order-details.helpers.ts:69-88`
   maps `3→status-inprogress, 4→status-completed, 5→status-cancelled, 6→status-ontheway` against
   `OrderStatus.cs:8-14` (`New=0 … Cancelled=6`), so a **Cancelled** order renders with the
   `pi pi-send` "on my way" icon and `New` falls to the `status-pending` default. Its explanatory
   comment (*"OnTheWay = 6 … appended numerically"*) is **factually false** — a false-mirror comment of
   exactly the species D7 exists to kill. **In scope for the take-gate ticket**: it is in the same file
   as surface #10, and this ADR introduces a refusal whose only self-service explanation is *"open the
   order and read its history"* — that history is this timeline.
6. **`AdminOverrideOrderStatus` has no payment guard** (CH-M4) — it can push a card order to
   `Confirmed` while `PaymentStatus` is `Pending`, which the 15-minute sweep then cancels. D1's new
   money conjunct makes such an order **un-offerable**, so no cleaner is harmed; the *state* is still
   incoherent and the guard is worth adding. **Not** the fix for CH-M4 (see A16).

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

Two challenger lanes, **19 findings**, 8 marked blocking by their authors. Full reports:
`agents/backlog/adr/challenges/0037-mechanism.md` (CH-M1…M10) and
`agents/backlog/adr/challenges/0037-experience.md` (CH-X1…X9). Summarised by id; the lead
re-verified every claim below at source before ruling (see `## Verdict` for what the challengers got
wrong).

### Lane 1 — MECHANISM

| id | The hole | Blocking (author) |
|---|---|---|
| **CH-M1** | D6 considered three candidate error keys and missed the two the same partner-facing family already uses for these exact statuses — `order.already_cancelled` / `order.already_completed` (`AdminOverrideOrderStatus.cs:83-94`), already localized on Android and iOS. One opaque key collapses three behaviourally different outcomes. | — |
| **CH-M2** | **The ordering guarantee D6 declares load-bearing does not exist.** `TakeOrder.Validator` has **two** `RuleFor` chains; `Cascade.Stop` is rule-level and FluentValidation 12.1.1 defaults `ClassLevelCascadeMode` to `Continue`. Both chains always run; the pipeline returns every failure; the transport semicolon-joins them under one `ErrorCode` into a string no client can resolve. Fires today: an unknown id yields `order.not_found; order.time_conflict`. | **YES** |
| **CH-M3** | **`New` + Cash is not retraction-free** — the ADR's whole argument for admitting it. `AutoCancelStaleRecurringOrders` cancels on `PaymentStatus == Pending` with **no `PaymentType` term**, hourly, and a recurring cash order sits at `New` for up to 7 days before being retracted at **T−1h**. Also: Fact 2's categorical about the only writer is wrong. | **YES** |
| **CH-M4** | **The predicate is asymmetric** — it payment-qualifies `New` but *trusts* `Confirmed` to imply paid. `Confirmed + Card + PaymentStatus.Pending` is reachable (admin override has no payment guard; declines stay `Pending` for retry) and the 15-min sweep has no `OrderStatus` term. A4/A5 reject `PaymentStatus` as *whole rules* — correctly — but never address the conjunction. | **YES** |
| **CH-M5** | Removing `Pending` from `AdminOverrideOrderStatus.Lifecycle` makes `Array.IndexOf` return **`-1`**, which passes the rank guard for **every** target and unlocks a **backwards** move to `New`. D5's safety argument was a sentence telling the implementer to think about it. | — |
| **CH-M6** | The "15 strings in 11 files" bill was traced against a key **absent from 5 of those 11 files**; the web namespace is `api.*` not `errors.*`; and the stated failure mode ("shows the raw key") is **false on web**, which is silent by design. | — |
| **CH-M7** | **The enforcement mechanism does not run.** `nx affected` selects nothing on a mobile-only or Domain-only diff; `frontend-ci`'s push trigger is scoped to `src/Cleansia.App/**`; `backend-ci` excludes both mobile trees; and Nx's declared inputs cannot reference paths above the workspace root, so a cached green is served even when selected. | **YES** |
| **CH-M8** | Three passages still assume the spare seat the owner has since deleted (`SpareSeatsPerOrder = 0`, `IsFullyAssigned` gone) — including D0's "nearly all of them", which D6's severity argument quotes. | — |
| **CH-M9** | `PaymentType` as discriminator fails safe but is closed to extension; the ADR records no obligation for the next enum member. | — |
| **CH-M10** | D3's "a NULL row is a 500 today" is overstated — `Order.CurrentStatus` already falls back to loaded history. The design is right; the evidence for it is wrong, in the section an implementer reads hardest. | — |

### Lane 2 — EXPERIENCE (what partners and customers actually see)

| id | The hole | Blocking (author) |
|---|---|---|
| **CH-X1** | iOS Partner resolves error keys **only** from the shared `CoreL10n` catalog (`ApiErrorLocalizer.swift:29-33`) and has zero `error.order.*` keys of its own — so a cleaner who loses a race reads a customer's sentence: *"No cleaners are available for that slot. Please pick another time."* The ADR's copy bill inherits the defect class. | **YES** |
| **CH-X2** | The seat ruling made the **other** key the common one: a just-taken job is `Confirmed`, so `IsOfferable` passes and the race produces `order.no_available_spots`, not `order.not_takeable`. The ADR budgets copy for the rare key and none for the modal one — whose sentence names capacity, not the race, and which the digest's un-shortlisted country-wide fanout manufactures ~N−1 times per push. | — |
| **CH-X3** | On partner **web** a refused take never clears the row — both call sites have **no error callback**, and the reloads sit inside the success branch. The ADR ships a brand-new refusal into the one client that cannot recover from it. Both mobile clients reconcile correctly. | **YES** |
| **CH-X4** | Reviewer step 6 is false for web (silent generic fallback), and the ADR's own cost exemplar `order.weekly_limit_reached` is **missing from partner web today** — the failure mode is already live and has never been caught. | — |
| **CH-X5** | **The census is ten, not eight.** The two missed surfaces are the web *buttons*, and one of them — `canTakeOrder` (`order-details.helpers.ts:108-115`) — is `{Pending, Confirmed}` ≡ `{Confirmed}`, so it **hides the Take button for `New`**, contradicting the ruling in the direction that hides work. The proposed parity spec covers three query files and would go green over both. | **YES** |
| **CH-X6** | `buildOrderStatusOptions` offers dead `Pending` and omits `New`, and any filter selection **replaces** the default list — so touching the filter at all deletes the entire cash pipeline from the board, irrecoverably. | — |
| **CH-X7** | The count-vs-list contradiction is **moved, not fixed**: the ADR aligns the status/payment term and leaves the **date floor** forked, and §D9.2(a) *creates* that divergence on mobile where today there is none. | **YES** |
| **CH-X8** | The sweep the ruling leans on cancels **silently** (the only cancel path with no customer notification), and `Cancelled` is **not terminal** — a late payment writes `Confirmed` over it (`HandlePaymentNotification.cs:254` short-circuits only on `Paid or Refunded`). D1 says "Terminal" and D6 leans on the word. | — |
| **CH-X9** | The web status timeline is off by one (`STATUS_CLASS_MAP`), so a **Cancelled** order renders with the **OnTheWay** icon — in the file the ADR is already opening, on the screen a refused cleaner lands on, under a comment that is factually false. | — |

**Both challengers also filed explicit "checked and found sound" lists** (mechanism `:494-553`,
experience `:412-463`) — Fact 1, Fact 3's mechanism in full, the `StaleOrderCleanupService`
refutation, surface #4 as the correct seam, A13, A10, A11, D9.2(a)+(b), the client literals, and the
`Q-AVAIL-03` implementation. Per protocol, silence is not assent — and those are the parts of this ADR
that survived attack untouched.



## Defense

> **Written by the lead, 2026-08-03**, the author instance having been released. Recorded honestly as
> such: nothing here is the author's own defense. Where the draft is indefensible it is **conceded and
> the artifact is changed**, not argued around; where a challenger is wrong the rebuttal cites the code
> the lead opened. Every `file:line` below was re-read in this session — none inherited from either
> report.

### CH-M1 — CONCEDE IN PART + REVISE (→ **D6.1**, A22, A23)

**Conceded, and the concession is forced by the ADR's own argument.** D6's first and strongest reason
for shipping the gate is *family consistency* — *"it is the only unguarded command in its own
family."* Verified that `AdminOverrideOrderStatus.Handler` refuses `Completed` → `OrderAlreadyCompleted`
(`:83-88`) and `Cancelled` → `OrderAlreadyCancelled` (`:89-94`), and that both constants exist
(`BusinessErrorMessage.cs:49-50`). **An author cannot invoke family consistency to justify the gate and
decline it for the gate's vocabulary.** Three keys ship: the two existing ones for the two terminal
states, one new one for the residue.

**Rebutted in part:** `order.not_yet_payable` for the `New`+Card row is **rejected** (A23). Two
grounds the challenger did not weigh. (1) **Disclosure** — a cleaner is not a party to the customer's
payment; "not payable yet" tells them the customer has not paid, which no other partner-facing error
does and which this refusal has no business saying. (2) **Falsity** — "try again in a minute" is wrong
for `OnTheWay`/`InProgress` and misleading for the modal `New`+Card case, where an abandoned checkout
never becomes payable; it is cancelled within ~1h15m. One opaque residue key, with the copy **bound in
the ADR** (*"This job is no longer available."*) so the naming argument cannot be re-run at
implementation time.

### CH-M2 — CONCEDE + REVISE. The strongest finding in either lane (→ **D6**, verification step 5, A19)

**Every link verified independently:** `TakeOrder.cs:38-45` and `:47-60` are two chains;
`Directory.Packages.props:26` pins FluentValidation **12.1.1**; nothing in the repo sets
`ClassLevelCascadeMode`; `ValidationPipelineBehavior.cs:38-48` maps **every** failure to
`Error(failure.ErrorCode, …)`; `CleansiaApiController.cs:93-99` groups by `Error.Code` and joins with
`"; "` — and every `MustAsync` in this validator carries FluentValidation's same default `ErrorCode`,
so two async failures collapse to one dictionary entry. `TakeOrder.cs:154` returns `false` for a null
order, so **an unknown id yields `order.not_found; order.time_conflict` in production today.**

**The challenger understated it, and the understatement is the reason this ranked first.** The damage
is not a wrong sequence — it is that **existence becomes inferable from the *pairing***: a missing
order returns two joined keys, a **held** order (ADR-0036) returns a bare `order.not_found`. A bare
`not_found` therefore proves the order exists and does not overlap the caller's calendar, defeating
precisely the protection ADR-0036 folded the hold into the existence rule to build. The draft
*asserted* that protection held and then spent that assertion to justify adding a fourth key to the
same response.

**Ruled on the mechanism, not the intent, as required.** Three candidate mechanisms were on the table
and the ADR now names one: **a single ordered `RuleFor(x => x).Cascade(CascadeMode.Stop)` chain**
(structural — D7 layer 1 applied to the validator), **enforced by `TC-TAKE-ONE-ERROR`** (exactly one
error per refusal scenario, the held order included). `ClassLevelCascadeMode = Stop` is recorded as an
accepted equivalent **only with the test** and is not preferred: action at a distance, invisible at
every rule site (A19). The comment-as-enforcement step 5 is deleted. **Side effect worth having:** the
same change closes the live unresolvable-composite defect — one change, two fixes — which is why it is
in scope for the take-gate ticket rather than a follow-up.

### CH-M3 — CONCEDE + REVISE, and the fix is the ADR's own method (→ **D1**, Fact 2, A17, A18)

**Verified end to end.** `AutoCancelStaleRecurringOrders.cs:63-69`: `RecurringTemplateId != null &&
PaymentStatus == Pending && CleaningDateTime <= now+1h && UserId != null` — **no `PaymentType`
term**, hourly, and `MaterializeRecurringBookings.cs:27,131` creates the occurrence up to 7 days ahead
carrying the template's payment type. Fact 2's categorical is wrong: `ConfirmRecurringOrder.cs:111-112`
also moves a cash order off `New`.

**The lead found the case worse than the challenger reported.** The sweep's in-memory guard skips only
`Cancelled`/`Completed` (`:78-82`). A recurring cash order that a cleaner has **already taken** is
`Confirmed` — and cash never leaves `PaymentStatus.Pending` until collection — so it **also** matches
and is cancelled at T−1h **with the cleaner on it**. Under the draft that path was reachable *through
the take gate this ADR ships*. It closes with the same term.

**Rebutted on the remedy.** The challenger offered "fix the sweep's predicate as part of this
decision" as one option; the lead rejects it (A17). On a recurring order `PaymentStatus == Pending`
**is** "the customer has not confirmed this occurrence" — `ConfirmRecurringOrder.HandleCashAsync`
writes `Paid` for **cash** too, so the sweep needs no `PaymentType` term and adding one would leave
cleaners standing on bookings the customer never accepted. **The sweep is correct; the offer was
wrong.** Nor is this an owner escalation (A18): every option that keeps them offerable ends with a
cleaner losing a booked slot an hour before it starts — strictly worse than the card case the ADR
already refuses on identical reasoning. There is no trade-off to price.

### CH-M4 — CONCEDE + REVISE (→ **D1**, A15, A16; adjacent finding filed)

**Verified.** `CleanupStalePendingOrders.cs:50-53` has **no `OrderStatus` term**;
`AdminOverrideOrderStatus.Handler` has **no payment guard** (only `:83-94` terminal and `:96-106`
rank); `HandlePaymentNotification.cs:230-242` deliberately leaves a **declined** card `Pending` for
retry. `Confirmed + Card + Pending` is reachable by two independent paths, and the sweep kills it out
from under an assigned cleaner.

The challenger's framing is exactly right and is why this was conceded rather than argued: **A4/A5
reject `PaymentStatus` as *whole rules* and both rejections are sound — but neither addresses the
conjunction, which is a different proposition** (A15). The lead unified CH-M3's and CH-M4's fixes into
one term rather than bolting on two special cases, because they are the same question:
`NotRetractable(o)` is the union of the negations of the two sweeps that actually run. That makes the
document's headline invariant — *"offerable when nothing still in flight can retract it"* — **literally
the predicate** instead of a slogan sitting above a predicate that did not test it. A16 records why the
alternative (guard `AdminOverrideOrderStatus` instead) is insufficient: it misses the decline path
entirely and puts this rule's correctness in another aggregate's validator.

**Adjacent finding accepted and filed, not folded:** the same over-broad sweep appears to kill **every
recurring card order** ~1h after materialization. Re-verified as a chain; it does not falsify this ADR
(such orders are `New`+Card → not offerable either way), so it is a defect, not a blocking challenge —
but the ADR is the document that promoted this sweep, so it carries the finding (§Escalations #1)
rather than leaving the next reader to re-derive it.

### CH-M5 — CONCEDE + REVISE, and the drafted action is replaced, not patched (→ **D5** action 2)

**Verified at `AdminOverrideOrderStatus.cs:96-101`.** With `Pending` removed from `Lifecycle`, a
`Pending` row yields `currentRank = -1`; every legal target satisfies `targetRank >= 0 > -1`, so the
guard passes for **all** of them, `New` at index 0 included. The forward-only invariant inverts for
exactly the legacy rows D5's "dead, not deleted" reasoning exists to protect.

**The lead rejects both remedies the challenger offered** ("refuse an off-lifecycle `currentStatus`"
or "pin it to the `New` rank") as the *primary* fix, because both accept the draft's premise. The root
cause is that **one array is answering two questions** — *what rank is this status* (needs `Pending`)
and *what may an admin target* (must exclude `Pending`). That conflation is the disease this ADR
exists to cure, applied to the cure. So: **`Lifecycle` keeps `Pending`** and a **separate explicit
target guard** rejects it — which achieves the draft's actual goal (no new writer) without touching
ranking. The `currentRank < 0` refusal is kept as a **secondary** guard precisely because it is
unreachable today: the next `OrderStatus` member forgotten in `Lifecycle` re-opens the backwards move
otherwise. Pinned by a seeded test, not by a sentence.

**Knock-on accepted:** `TakeOrder.cs:192`'s `or OrderStatus.Pending` arm becomes unreachable once the
gate ships. **Deleted**, with the reason recorded so it is not confused with the "readers keep
tolerating `Pending`" rule — that rule covers *conservative-direction* readers, and this is a
status-**write** trigger.

### CH-M6 / CH-X4 — CONCEDE + REVISE (→ **D6.3**, verification step 6, §Escalations #3 and #4)

**Verified independently.** `rg 'weekly_limit_reached|no_available_spots'` across all five
`cleansia-partner.app` locale files returns **only** `no_available_spots` (`:1075` in each) —
`weekly_limit_reached` is absent from every one, while Android, iOS and customer web carry it. It is
thrown by `TakeOrder.cs:57-58`, this very validator. The namespace is `api.*`
(`http-error.interceptor.ts:15`, one shared interceptor for all three web apps), and a missing key
renders `api.common.error_occurred` — **silent**, by deliberate design (`:14-20`).

**All three corrections stand and are folded in.** The one the lead weighs heaviest is not the missing
string: it is that **the draft's only proposed detection for 15 strings was a human running step 6, and
on the one client where the failure is invisible, step 6 said it was visible.** Step 6 is now a set of
greps. The root `CLAUDE.md`'s `errors.*` instruction is wrong and is routed to the docs agent — a
project guide that misdirects developers into a namespace that never resolves is a rule that needs
changing, not routing around.

### CH-M7 — CONCEDE + REVISE. The ADR claimed coverage it did not have (→ **D7** layer 2, verification step 7)

**All four legs verified.** `frontend-ci.yml:86` is `nx affected -t test`; its push trigger is
paths-scoped to `src/Cleansia.App/**` (`:12-17`); `backend-ci.yml:14-19,23-28` explicitly excludes both
mobile trees; and `nx.json`'s `@nx/jest:jest` inputs are `["default", "^production",
"{workspaceRoot}/jest.preset.js"]` with `default = ["{projectRoot}/**/*", "sharedGlobals"]` and
`sharedGlobals = []` — so no file outside `src/Cleansia.App` is a declared input and a cached green is
reachable. **There is no workflow that fires on a mobile-only change and can read C#.**

The challenger's judgement is accepted verbatim: *a test with no trigger is a comment with a `.spec.ts`
extension, and it is more dangerous than a comment because the ADR records it as the thing that makes
the ruling durable.* **The lead went further than the challenger's minimum.** The proposed fix
(unconditional step + widened triggers) is *configuration*, and configuration regresses: a later
`nx.json` edit or a trigger tidy-up silently re-opens it. The ruling makes it **structural** — a plain
Node script outside the Nx workspace, which **cannot** be cached-green by construction, with **its own
repo-root workflow** so widening `frontend-ci`'s paths does not spin three Angular production builds on
every Swift commit. And per CH-X5 it covers **button gates**, not only query literals. The escape hatch
is explicit and non-negotiable: **if that is not delivered, D7 layer 2 is written down as ADVISORY.**
An ADR may under-claim; it may not over-claim.

**Partial rebuttal on layer 3.** "No CI wiring for `check-consistency.mjs`" is true but is the
**documented design** — `process/enforcement.md:16,83,95` places it at T2-ADVISORY, Reviewer-run, with
CI promotion gated on a clean baseline. So it does not blunt layer 3's stated role. It does, however,
supply a second reason layer 3 cannot substitute for layer 2, which the challenger missed: that tool's
walker globs `.cs`/`.ts`/`.kt` **only — no Swift** (`enforcement.md:17`). It is structurally incapable
of seeing the iOS literal.

### CH-M8 — CONCEDE + REVISE (→ D0, D1's `Confirmed` row, D9.2(b), D9.4 interim, D9.5)

**Verified:** `BookingPolicy.cs:76` `SpareSeatsPerOrder = 0`; the formula moved to
`Order.CalculateRequiredEmployees` (`Order.cs:534`), one production caller (`OrderFactory.cs:148`);
`rg IsFullyAssigned src/` → **zero hits**; `Order.UnassignEmployee` has **one** production caller
(`AdminReassignOrder.cs:86`), so a fulfilled `Completed` order has no free seat and is already
invisible to the `RestrictToEmployeeId` floor.

**All four restatements folded in. No conclusion flips; three severity claims do**, and the
challenger's closing argument is the reason this was treated as more than tidying: *an implementer who
catches one falsified premise stops trusting the other forty citations.* A document whose authority is
"Gate 0: every row read" is disproportionately damaged by stale evidence. Also folded: D1's `Confirmed`
row now cites its **real** generator (`ConfirmRecurringOrder.cs:111-112` — recurring cash → `Confirmed`
+ `Paid`, no assignment) instead of a spare seat that no longer exists — and that citation turned out
to be load-bearing for CH-M3's fix, which is a good illustration of why stale evidence is not cosmetic.

### CH-M9 — CONCEDE + REVISE (→ **D3**, verification step 8c)

Accepted as framed, including the challenger's own restraint (*"I am not asking for an abstraction"*) —
switching on `PaymentType` is idiomatic here and `OrderPaymentDispatcher.cs:71-72` /
`ConfirmRecurringOrder.cs:100-101` already carry `default:` arms. One exhaustiveness test over
`Enum.GetValues<PaymentType>()`, red on a new member. **The panel's own amendment raises its value**:
a new payment type must now be classified on **two** axes (offerable at `New`? retractable by which
sweep?), so the number of ways to get it silently wrong went up, not down.

### CH-M10 — CONCEDE + REVISE (→ **D3**)

**Verified:** `Order.cs:284-289` already falls back to the loaded history ordered by
`(CreatedOn desc, Sequence desc)`; `TakeOrder.Handler` includes it (`:179`); `OrderFactory.cs:166`
writes a `New` track at creation. The 500 needs a NULL column **and** zero loaded history rows.
Sentence corrected; **the design is kept exactly as drafted**, as the challenger explicitly asked.
A wrong `file:line` in the section an implementer reads hardest is worth a concession on its own.

### CH-X1 — CONCEDE the defect, **REBUT the diagnosis**, and the cheaper fix wins (→ **D6.3**, A20)

**The defect is real and blocking.** `ApiErrorLocalizer.swift:29-33` resolves `"error." + key`
**only** from `CoreL10n.bundle` and never probes the app bundle; `Localizable.xcstrings:3502-3532`
carries `error.order.no_available_spots` en = *"No cleaners are available for that slot. Please pick
another time."* A cleaner who loses a race reads a sentence that is untrue (they are the cleaner) and
unactionable (there is no time for them to pick).

**The diagnosis is wrong, and correcting it makes the fix ~10× cheaper.** The challenger framed this
as *"one file with two personas and no separator"* and demanded either an app-bundle probe or
persona-neutral copy as a hard constraint. **But no customer-facing command can emit this key.**
`rg NoAvailableSpots src --type cs` returns exactly two production emitters — `TakeOrder.cs:45`
(partner) and `AdminReassignOrder.cs:95` (admin). `BookingSubmitOutcome.swift:7-10`, cited as evidence
that the string belongs to the booking flow, is a **doc comment listing example keys** — not a binding,
and not a path the server can drive. The string is **mis-authored, not persona-collided.**

So: **re-voice the five strings** (the same edit CH-X2 already requires) and **do not add a lookup
path** (A20) — a second resolution order plus a per-app override seam, itself needing a parity guard,
to fix one sentence. The durable half of the finding is kept as a **catalog rule**: *a shared-catalog
key must be voiced correctly for every persona that can receive it; if two personas need different
sentences for one key, the **backend emits two keys*** — which preserves the per-audience host seam
instead of teaching a shared localizer about audiences.

### CH-X2 — CONCEDE + REVISE, in scope (→ **D6.2**)

**Verified by tracing the ADR's own mandated order against the shipped seat cap.** A just-taken job is
`Confirmed` (`TakeOrder.cs:192-194`), so `IsOfferable` **passes** and the refusal falls to
`HasAvailableSpots` → `NoAvailableSpots` (`:44-45`). **The race never produces `order.not_takeable`.**
It produces the key D6's table dismissed as *"Lies. The seat exists"* — which after
`SpareSeatsPerOrder = 0` is itself false on the modal booking. And `NewJobsDigestService.cs:62-74`
selects **every** approved cleaner in the country with no radius, cap or shortlist, so the losing
message is manufactured ~N−1 times per push.

Ruled **in scope** rather than filed, on two grounds the challenger did not have to argue: the ADR
would otherwise knowingly ship copy work aimed only at the rare key, and **the reword is the same edit
that fixes CH-X1 on iOS** — one string, two defects, in files the ticket already has open. Recorded
limit: admin copy is **not** re-voiced (`AdminReassignOrder` genuinely is out of seats, and admin has
its own locale files) — the per-audience seam doing its job.

### CH-X3 — CONCEDE + REVISE, in scope (→ **D6.4**, verification step 8b)

**Verified.** `orders.facade.ts:207-218` subscribes with **no error callback** and both reloads sit
inside `if (response)`; it is also the only call in that facade lacking `takeUntil(this.destroyed$)`.
`order-details.facade.ts:189-202` swallows via `catchError(() => of(null))`. Both mobile clients
reconcile correctly and their own comments name the scenario.

Ruled in scope. D6's cost argument is *"~6 lines against a validator being opened anyway"*, and **the
honest bill includes the client that has to survive the new refusal.** Adding a refusal path to a UI
with no reconcile is not "the implementer will notice" — it is a defect with a design decision standing
in front of it. That is precisely what a panel is for.

### CH-X5 — CONCEDE + REVISE. The census was wrong and the miss contradicts the ruling (→ **D0**, **D4** rows 9–11, **D7** layer 2)

**Both surfaces verified at source.** `orders.models.ts:169-176` carries a fourth web status literal
including dead `Pending`. `order-details.helpers.ts:108-115` `canTakeOrder` is
`Pending || Confirmed` — and by the ADR's own Fact 1 that is `{Confirmed}`, so **the detail page hides
the Take button for a `New` cash order**: the exact case Fact 2 calls the strongest single argument for
the ruling. It is listed on the board, takeable by the server, and unclickable on its own page. Both
mobile clients get it right (`OrderPrimaryAction.swift:44-48`, `OrderPrimaryAction.kt:57-58`), which is
what makes it a defect rather than a design choice.

**The sharpest part of this finding is what it says about D7**, and the lead has folded it into the
enforcement ruling: *a parity check that covers the query and not the button tests the wrong half.* The
query decides what is **listed**; the button decides what is **clickable**; this ADR's entire thesis is
that those must not diverge — and the drafted three-file spec would have gone **green** over surface
10. Rows 9 and 10 are in the census and in the parity check.

### CH-X6 — CONCEDE + REVISE, in scope (→ **D0** row 11, **D4** row 11)

**Verified** at `orders.helpers.ts:49-56` (offers `Pending`, omits `New`) against
`orders.facade.ts:142` (`additionalFilters?.orderStatuses || [...]` — any selection **replaces** the
default). Ruled in scope: three lines, in a file already being edited for the same reason, and after
this ADR the cliff gets **worse**, because `New`+Cash becomes the canonical pre-take state of the whole
cash pipeline and is the one state the cleaner's filter cannot name. Leaving it would also keep a
dropdown option that is guaranteed to return zero rows — a UI affordance for a dead status, shipped by
the document declaring it dead.

### CH-X7 — CONCEDE + REVISE, and the fix is a named constant, not a note (→ **D4.1**, A21)

**Verified.** `DashboardSpecifications.cs:18` is `cleaningDateFrom: null`; `GetPagedOrders.cs:57-61`
applies `now − 2h` **only** when `HasAvailableSpots == true`; `orders.facade.ts:149` pins `now`,
stricter still. Today mobile's count and list agree on the date axis **by accident** — neither has a
floor. **§D9's client switch breaks that accident**, so the ADR would newly introduce "N available
jobs" above a list of N−k on two clients that agree today.

**The challenger's option (b) is rejected** (A21): documenting the count as deliberately floor-free is
D7's weakest layer wearing a different hat, and a count a cleaner cannot reconcile with the list
beneath it is the defect T-0530 exists to close. **Option (a) is adopted and generalized**: the `-2h`
literal becomes a named policy number, the dashboard spec reads it, **and web stops sending its own
floor** — otherwise the "one rule, one place" fix leaves a third answer in a client. The behaviour
change on partner web (jobs up to 2h past become visible, as on mobile) is small, deliberate, and
recorded with its flip condition: change the constant once, not one client.

### CH-X8 — CONCEDE in part; the word is wrong, the model is not (→ **D1** `Cancelled` row, verification step 8, §Escalations #2)

**(b) verified and conceded.** `HandlePaymentNotification.cs:254` short-circuits only on
`PaymentStatus is Paid or Refunded`, and the sweep leaves `Failed` — so a late payment writes
`Paid` + a `Confirmed` track over a cancelled order. D1's row is **qualified**: terminal *for this
predicate at the moment it is evaluated*, not terminal in the lifecycle.

**Rebutted in part on consequence.** The challenger implies the model is broken; it is not — **it is
the amended rule working**. That order is genuinely live and genuinely paid, so it *should* re-enter
the offerable set; the predicate is stateless and re-evaluates on the latest track, and the new
`NotRetractable` term is satisfied by `PaymentStatus == Paid`. What was wrong was one word and the
implication that any surface may cache "has ever been cancelled". Both fixed.

**(a) accepted and filed** (§Escalations #2): `CleanupStalePendingOrders` (`:69-79`) is the only
production writer of `OrderStatus.Cancelled` that dispatches no `OrderCancelled` notification — both
siblings do. A customer-facing hole under this ADR's central premise; a defect, not a decision.

**Test ask split.** The sweep case is **added to AC4** — the challenger is right that the one behaviour
making the card ruling defensible was asserted and never tested, and it is cheap. The late-payment
resurrection is **declared out of scope in writing** (the challenger offered that as acceptable): it
exercises Stripe webhook handling, and the predicate handles it by construction.

### CH-X9 — CONCEDE + REVISE, fix in-flight (→ §Escalations #5)

**Verified** against `OrderStatus.cs:8-14` (`New=0 … Cancelled=6`): `STATUS_CLASS_MAP`
(`order-details.helpers.ts:69-88`) is shifted by one, so `6` (**Cancelled**) paints `status-ontheway`
with `pi pi-send`, `5` (Completed) paints `status-cancelled`, and `New=0` falls to the
`status-pending` default. Its explanatory comment is factually false.

Ruled **fix in-flight**, not merely filed, on the challenger's own argument: the ADR introduces a
refusal whose only self-service explanation is *"open the order and read its history"* — and that
history is this timeline. It is in the same file as surface #10, which the ADR now edits. **It is also
a false-mirror comment**, the exact species D7 exists to kill, sitting inside the document that kills
them.

## Verdict

> **CONSENSUS REACHED — 2026-08-03, lead. Zero blocking challenges remain. Status → `accepted`.**

**Disposition of all 19 findings:**

| Ruling | Findings | Count |
|---|---|---|
| **CONCEDE + REVISE** (artifact changed) | CH-M1*, CH-M2, CH-M3*, CH-M4, CH-M5*, CH-M6, CH-M7, CH-M8, CH-M9, CH-M10, CH-X1*, CH-X2, CH-X3, CH-X4, CH-X5, CH-X6, CH-X7*, CH-X8*, CH-X9 | **19** |
| of which **partially rebutted** (`*`) — the finding stands, a stated remedy or diagnosis does not | CH-M1 (A23), CH-M3 (A17/A18), CH-M5 (remedy replaced), CH-X1 (A20 — diagnosis corrected), CH-X7 (A21), CH-X8 (consequence) | 6 |
| **REBUT outright** | — | **0** |
| **ESCALATE to the owner** | — | **0** |

**Rulings on the eight the challengers marked blocking — all RESOLVED:**

1. **CH-M2 — RESOLVED.** The ordering guarantee did not exist and was declared load-bearing; worse, the
   two-chain behaviour makes existence inferable from the error *pairing*, inverting ADR-0036's
   protection. **Ruled on the mechanism:** one ordered chain (structural), pinned by
   `TC-TAKE-ONE-ERROR` (enforcement that fails when violated). The comment-as-enforcement verification
   step is deleted. `ClassLevelCascadeMode` recorded as a lesser accepted equivalent (A19).
2. **CH-M3 — RESOLVED.** `New`+Cash is not retraction-free for **recurring** orders. **The sweep is not
   changed** (A17) — its predicate is semantically correct; the offer was wrong. The predicate gains
   `NotRetractable`. Not escalated (A18): every alternative ends with a cleaner losing a slot at T−1h.
   The lead additionally closed a path neither party named — a *taken* recurring cash order is swept
   too, and the same term closes it.
3. **CH-M4 — RESOLVED.** Offerability now carries a money term on **both** branches. A4/A5 stand as
   whole-rule rejections and are re-argued against the conjunction (A15). The adjacent recurring-card
   sweep finding is filed (§Escalations #1), not folded — it does not falsify the ruling.
4. **CH-M7 — RESOLVED.** The enforcement did not run and the ADR claimed coverage it lacked. Ruled
   **structural** (a plain non-Nx script with its own repo-root trigger, covering button gates), with
   a behavioural acceptance test and an explicit fallback: **if not delivered, layer 2 is labelled
   ADVISORY.** An ADR may under-claim; it may never over-claim.
5. **CH-X1 — RESOLVED.** Defect conceded, **diagnosis corrected**: no customer-facing command emits
   `order.no_available_spots`, so the string is mis-authored, not persona-collided. Re-voice the five
   strings; **do not** add an app-bundle probe (A20). The structural value is kept as a catalog rule —
   *if two personas need different sentences for one key, the backend emits two keys.*
6. **CH-X3 — RESOLVED.** The web reconcile is **in scope** for the take-gate ticket. Shipping a refusal
   into the one client that cannot recover from it is not an implementation detail.
7. **CH-X5 — RESOLVED.** The census is **ten**; surface 10 (`canTakeOrder`) contradicts the ruling by
   hiding Take for `New`, and the drafted parity spec would have gone green over it. Rows 9–11 added
   with verdicts; the parity check covers **button gates**, not just query literals.
8. **CH-X7 — RESOLVED.** The count/list divergence was moved, not fixed, and §D9 *creates* it on
   mobile. One named date-floor constant read by the count, the list and (by removal) the web client.
   Documenting the divergence was rejected (A21).

**Also ruled, on the lead's own reading rather than either report:**
- **CH-M5 is upgraded to blocking-as-drafted** (neither challenger marked it so). D5's own cleanup
  would have inverted a shipped admin safety guard for exactly the legacy rows it was written to
  protect, with a sentence as its control. The remedy is not the one offered: `Lifecycle` keeps
  `Pending` and a **separate** guard rejects it as a target — because one array answering two
  questions is this ADR's own thesis, violated by its own action list.
- **CH-M3's blast radius is larger than reported** — an already-taken recurring cash order is swept at
  T−1h with the cleaner assigned. Closed by the same term.

**What survived attack unchanged, and is therefore the load-bearing core:** Fact 1 and Fact 3; the
`StaleOrderCleanupService` refutation; **surface #4 as the correct seam** (A13 re-verified by both
lanes); the ruling that offerable and takeable are one set read at two moments (D2); `OrderAvailability`
in `Core.Domain` with **two evaluation forms + an equivalence test** and no `.Compile()` (D3, A10);
`Pending` deprecated-not-deleted (A11); the take gate shipping at all (D6); and **§D9.4's
property-not-formula rule, which has already been tested by the owner's `Q-AVAIL-03` flip and held —
one line changed, six reading surfaces untouched.**

**The one structural lesson this panel produced, recorded for the catalog:** *the draft stated a
correct invariant in prose and shipped a predicate that did not test it.* Three of the four blocking
mechanism findings are instances of the same failure — an assertion of a property (rule ordering,
retraction-freedom, CI coverage) with no artifact that fails when the property stops holding. **That is
the same disease as a comment claiming two lists agree**, which is the disease this ADR was written to
cure. It is now folded into `agents/knowledge/patterns-backend.md` as a rule about invariants:
**if you write down an invariant, write down the thing that goes red when it breaks — in the same
change.**

**Consensus.** Zero blocking challenges remain. Nothing here required an owner decision; the two
questions that did (`Q-AVAIL-01`, `Q-AVAIL-03`) were answered before the panel convened, and
`Q-AVAIL-02` remains recorded-as-decided with a named flip condition. **ADR-0037 is `accepted` and
immutable from this point** — deviations need a superseding ADR. The PM may ticket; the six defects in
§Escalations are **not** part of T-0530 and need their own rows.
