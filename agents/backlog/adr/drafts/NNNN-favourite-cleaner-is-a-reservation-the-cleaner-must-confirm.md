# ADR-NNNN — The favourite cleaner is **assigned**, and assignment is a **reservation they must confirm**: ADR-0036's exclusivity mechanism is kept **byte-for-byte** and given the four things it lacks — a **name**, an explicit **decline**, a **customer-visible state**, and a **customer-facing exit** — where the reservation is **never an `OrderEmployee` row**, the release stays a **clock comparison with no actor**, the **confirm is `TakeOrder` unchanged**, and the **only new actor is a 5-minute sweep that ANNOUNCES the lapse and releases nothing**; the customer's re-offer is **capped at two rounds** (Invariant H relaxed 90% → **80%**) and *"a random cleaner"* is ruled to be **the open board, named** — not a second dispatch model

- **Status:** `proposed` — drafted 2026-08-08 by the `architect` in **author** mode, on direct owner
  instruction recorded as the answer to `Q-PROMISE-02`. **Not yet challenged.** Numbers are allocated at
  acceptance; this file lives in `drafts/`.
- **Date:** 2026-08-08 (drafted)
- **Partially supersedes ADR-0036** — in **exactly three places**, enumerated in §D0. Everything else in
  ADR-0036 stands **unchanged and is relied upon**: D1's exclusivity, D2's stored-deadline storage rule
  and its aggregate-owned pair, D3's formula/floor/ceiling, D4 and D4.1's *notify on a wider predicate*,
  D5's five terms · two forms · six surfaces, D5.2's error-key rule, D5.3's digest freshness clause,
  D7's Plus gate, D8's recurring carry-through.
- **Does not touch ADR-0037** (offerability). No new `OrderStatus`, no new arm in `OrderAvailability`,
  no new conjunct anywhere. The one new read surface **conjoins** `IsOfferableSql` exactly as the digest
  already does (`NewJobsDigestService.cs:137-138`). See §D9.
- **Does not touch ADR-0039** (slot availability) — it is the **precondition**. *"Has a free spot"* is
  ADR-0039's answer, asked at ADR-0039's two moments, through ADR-0039's one method
  (`OrderRepository.GetBusyEmployeeIdsInWindowAsync`, `:287-310`). See §D1.
- **Composes with** ADR-0035 (the express waiver — §D8 is where the two perks collide over a
  cancellation), ADR-0002/0008 (outbox, unchanged), ADR-0025 (push display, unchanged), ADR-0040
  (`CurrentStatus` NOT NULL — relied on by the sweep's predicate).
- **Applies to:** `Cleansia.Core.Domain` (**two nullable/defaulted columns on `Order`**, one widened
  aggregate method, one derived state function — **no new entity, no new status, no change to
  `OrderVisibility`, no change to `OrderAvailability`**) · `Cleansia.Infra.Database`
  (⚠️ **`ef-migration`, owner-only**; additive, **no backfill**, **no new index**) ·
  `Cleansia.Core.AppServices` (one new partner command, one new partner query, one new customer
  command, one shared notifier, one sweep handler, three `BookingPolicy` constants) ·
  `Cleansia.Functions` + `Cleansia.Functions.Core` (**one timer**) ·
  `Cleansia.Web.Partner` + `Cleansia.Web.Mobile.Partner` (the decline + the pending-offers list) ·
  `Cleansia.Web.Customer` + `Cleansia.Web.Mobile.Customer` (the re-offer + the state on order detail) ·
  ⚠️ **`nswag-regen`, owner-only** (the customer order DTO gains a nested block; two new endpoints) ·
  the three customer clients and the three partner clients · **no host coupling** — each host gains only
  the endpoints for its own audience, over the shared Core.
- **Owner input this ADR executes (verbatim, 2026-08-07, `questions/open.md:1226`):**
  > *"if an employee has a free spot then it has to work in a way that he has to be assigned, not just
  > set a priority. There is a need to check also the functionality around it for both employee and
  > customer. And send a notification to the employee when customer created an order and then ask
  > employee to confirm the order; if not then to offer customer either select another employee that
  > will go through the same flow of approval, or suggest a random cleaner."*

---

> ## AC1 — the ruling, in one sentence a test can check
>
> **From creation until `Order.PreferredHoldUntilUtc`, the order's seats are reserved for
> `Order.PreferredEmployeeId` alone — exactly as ADR-0036 already makes them — and that reservation is
> now (a) named to the customer as an assignment awaiting confirmation, (b) refusable by the cleaner in
> one tap, and (c) followed, at the instant it ends without a confirmation, by exactly one message to
> the customer offering a second and final choice.**

> ## AC2 — the property that keeps ADR-0036's safety argument intact
>
> **No sweep, timer, handler or human ever releases a reservation. The release is, and remains,
> `PreferredHoldUntilUtc <= now` inside a `WHERE` clause (ADR-0036 D2 consequence #1).** The new sweep
> writes exactly one column — a notification receipt — and if it never runs, every order still opens to
> the whole board on time. **A dead sweep costs a prompt, never a booking.**

> ## AC3 — the sentence that replaces ADR-0036's AC3
>
> ADR-0036 AC3 said: *"No assignment-model change is required, anywhere, for any order… `TakeOrder`
> remains the only path by which an order acquires a cleaner."* **The second half survives verbatim and
> is load-bearing; the first half is superseded.** `TakeOrder` is still the only path by which an order
> acquires a cleaner — **the cleaner's "Confirm" button IS `TakeOrder`, unchanged, with a different
> label** — but the *model* the platform presents, and acts on, is now assignment-with-confirmation:
> the order is reserved for one named person, they may accept or refuse, the customer is told, and the
> platform acts when nobody answers.

---

## Context — every citation below was re-verified by reading, 2026-08-08

### The brief's premise about notifications is stale, and the correction shrinks this ADR

The task brief states *"the partner notification surface: today the **only** partner-targeted dispatch
is a 30-minute new-jobs digest."* **That was true when ADR-0036 was drafted and is no longer true.**
Verified:

| Partner-targeted event | Where |
|---|---|
| `order.new_available` (the 30-min digest, a **count**) | `NotificationEventCatalog.cs:30` |
| **`order.preferred_offer`** (**one order**, produced inline in the create path) | `NotificationEventCatalog.cs:44`; produced at `OrderFactory.cs:192-202` |
| `order.assignment_cancelled` | `NotificationEventCatalog.cs:52` |
| `payroll.invoice_paid` | `NotificationEventCatalog.cs:60` |

`NotificationFeedEventKeys.Partner` (`:47-53`) lists all four. **The per-order, time-sensitive,
digest-bypassing partner push the owner asks for is already shipped**, with its own documented
contract: it does not stamp `Employee.LastNewJobsDigestAt`, and it rides
`NotificationCategory.NewJobsAvailable` so a muted cleaner is not pushed around their own mute
(`NotificationEventCatalog.cs:32-44`). **This ADR adds ZERO partner-targeted notification events**
(§D10), which is why the notification cost the brief anticipated is not this decision's cost.

### What else already ships (the whole of ADR-0036 and ADR-0039 landed while their living doc still said "nothing is shipped yet")

| Thing | Evidence |
|---|---|
| The hold pair + its sole writer | `Order.cs:246`, `:264`, `GrantPreferredHold` `:424-435`, `ClearPreferredHold` `:438-443` |
| The one visibility rule, both forms | `OrderVisibility.cs:36-52` |
| The resolver, all eight gates in order | `PreferredCleanerHoldResolver.cs:23-101` |
| The window formula, floor and ceiling | `BookingPolicy.ComputePreferredHold` `:171-180`; `PreferredHoldFraction = 0.10m` `:159`; `PreferredHoldCeilingHours = 12` `:160`; floor `2 * StandardLeadTimeHours` where `StandardLeadTimeHours = 4` (`:20`) |
| The targeted push | `OrderFactory.cs:175-202` |
| Enforcement at the six surfaces | `OrderSpecification.cs:169-173`; `OrderAccessService.cs:88-91`; `TakeOrder.cs:83-91`; `NewJobsDigestService.cs:138` |
| The digest's hold-expiry freshness disjunct | `NewJobsDigestService.cs:261-275` |
| ADR-0039's set-based occupancy + the picker tri-state | `OrderRepository.cs:287-333`; `GetMyServingCleaners.cs:45-46`, `:92-145` |
| The Plus gate at creation | `CreateOrder.cs:162-171` |
| Recurring carry-through | `MaterializeRecurringBookingTemplate.cs:240` |

**So the owner's five asks decompose against shipped code as follows, and this is the single most
valuable paragraph in this document:**

| Owner's ask | State |
|---|---|
| *"he has to be **assigned**"* — the order is his to lose, not one row among many | **Mechanically SHIPPED.** ADR-0036 withholds the order from every other cleaner until the deadline. What is missing is that nobody — not the cleaner, not the customer — is ever *told* this. |
| *"send a notification to the employee when customer created an order"* | **SHIPPED.** `order.preferred_offer`, `OrderFactory.cs:194`. |
| *"ask employee to **confirm** the order"* | **SHIPPED as a mechanism, MISSING as a surface.** "Confirm" is `TakeOrder`. The held order appears only on the cleaner's ordinary available board, which is exactly why it reads as *priority*, not *assignment*. |
| *"if not…"* — an explicit refusal | **MISSING.** There is no decline. Silence is the only refusal, and it is indistinguishable from not having looked. |
| *"…then offer customer either select another employee… or suggest a random cleaner"* | **MISSING entirely.** The customer is told nothing at any point, and there is no way to change a preference after booking. |

**Two of five are missing, and one is a surface.** That is the real size of this feature, and it is why
this ADR spends most of its length protecting what exists rather than building.

### The tension the owner's words create, named precisely

> A **hold** is an *opportunity*: nobody else may take it, and if you do nothing it quietly goes away.
> An **assignment** is an *obligation with an escape hatch*: it is yours unless you say otherwise.

They are observationally identical **to the board** — in both cases no other cleaner may take that seat
for the window. They differ in four things and only four:

1. **Who is told.** A hold is invisible by construction (ADR-0036 D4: *"exclusivity is invisible to the
   excluded — a board is a query result, not a diff"*). An assignment must be visible to the
   beneficiary **and** to the customer, or the word is marketing.
2. **What silence means.** Under a hold, silence is *not taking*. Under an assignment, silence is a
   **failure to answer** — which is a thing the platform must be able to observe and act on.
3. **Whether refusal is expressible.** A hold has no decline. An assignment must have one, or the
   customer's exit is gated on a timer that could have ended in seconds.
4. **Whether the customer gets a next step.** A hold ends into silence. An assignment must end into a
   choice.

**None of those four requires changing how exclusivity is achieved.** That is the whole design.

### The word the owner did *not* use, and why it decides the shape

The owner said *"has to be **assigned**"*, then *"ask employee to **confirm**"*. **Those two words
together are not "assign" in the dispatch sense — they are a reservation.** If the cleaner must
*confirm*, then their silence leaves the job un-theirs; the platform is not *giving* them the job, it is
*holding* it for them and asking. A true assignment (the job is theirs whether or not they answer, and
the customer's cleaner arrives) is a different product and a different set of failure modes — a cleaner
who never read the push turns up nowhere, and the platform has promised a person it cannot deliver.

**This ADR builds the reservation, not the true assignment**, because the owner's own second clause
demands it. §Escalations records the one question this leaves open.

---

## D0 — What this supersedes in ADR-0036, exactly, and what it does not

**Three sentences and one number. Nothing else.**

| # | ADR-0036 text | Disposition |
|---|---|---|
| 1 | **AC3**: *"No assignment-model change is required, anywhere, for any order… this ADR changes who may see and take an order for a bounded interval, never whom an order belongs to."* | **SUPERSEDED in its first clause, KEPT in its second.** Whom an order belongs to still changes only at `TakeOrder`. What changes is that the interval is now named, refusable, disclosed and followed by a customer choice. |
| 2 | **D2 consequence #1**: *"Expiry needs no actor… There is **no sweep, no timer**, no outbox message, no status transition, no `IsActive` flip."* | **SUPERSEDED in its NOTIFICATION half only.** The **release** keeps every word: no sweep releases anything, no status moves, no column flips. A timer now **observes** expiry to notify the customer, and writes one receipt column it is the sole writer of. §D6 shows why the failure modes do not merge. |
| 3 | **D3 / Invariant H**: *"≥ **90%** of every seat's fill window is open to the entire board."* | **RELAXED to ≥ 80%**, enforced by a round cap of 2 rather than by the formula. §D5. This is the one genuine marketplace cost this ADR pays and it is escalated. |

**Explicitly NOT superseded, and each is relied on below:** D1 (exclusivity, not priority) · D2's storage
rule, the aggregate-owned pair, and the fail-OPEN-at-both-ends posture · D3's formula, the 8-hour floor
(**owner-ruled on CH-2; not reopened**), the 12-hour ceiling · D4 and **D4.1** (notify on a wider
predicate than the reservation) · D5 (five terms, two forms, six surfaces) · D5.0's two-forms-plus-an-
equivalence-test rule · D5.2 (never introduce an error key that names the exclusivity) · **D5.3** (the
digest's bounded disjunctive freshness clause — §D6 shows it is a hard constraint on the sweep) · D7
(Plus-only) · D8 (recurring carry-through).

---

## Decision

### D1 — "Has a free spot" is **ADR-0039's answer, unchanged**, asked at ADR-0039's two moments and nowhere else

The owner's *"if an employee has a free spot"* is already a settled predicate:

> a cleaner has a free spot for a booking iff they hold **no live-commitment assignment overlapping
> `[cleaningUtc, cleaningUtc + estimatedMinutes)`** — `OrderRepository.LiveCommitmentsInWindow`
> (`:318-333`), whose status set is `{New, Pending, Confirmed, OnTheWay, InProgress}` (`:264-271`) —
> **and** the seven other resolver gates pass (`PreferredCleanerHoldResolver.cs:32-90`: preference set ·
> signed in · active membership · cleaner exists · `IsActive` + `ContractStatus ∈ {Approved, Active}` ·
> work country matches the service address · category not muted · at least one device with
> `NotificationsEnabled`).

**It is evaluated at exactly two moments, and this ADR adds no third.** The picker
(`GetMyServingCleaners.cs:135-136`) and the resolver (`PreferredCleanerHoldResolver.cs:108-112`) call
**the same method with the same window** — ADR-0039 AC2, which is what makes the customer-facing claim
honest. A third evaluation moment would produce a third answer.

**What if the slot fills between booking and confirmation?** *(the brief's question, and it is the one
place a reader will reach for a new mechanism)*

**Nothing re-checks it, deliberately, and nothing needs to.** Between the grant and the confirmation the
beneficiary can only become busy by taking another job — and the confirm **is** `TakeOrder`, whose
single ordered chain already ends in `NotHaveTimeConflictAsync` (`TakeOrder.cs:70-71`, `:212-228`).
So a beneficiary who took a conflicting job is refused by the gate that already exists, with the error
that already exists (`BusinessErrorMessage.TimeConflict`). **The failure is self-correcting at the write
gate. Adding a creation-time or read-time re-check would be a second occupancy predicate, which
ADR-0039 D3.2 declares a hard reject.**

#### D1.1 — Taking a conflicting job **is** a decline (one write, on a path already writing)

The one residual: the beneficiary takes job B at T+10 min, and the customer's job A stays reserved for
them until T+2h24 for a person who can no longer confirm it. Under ADR-0036 that was accepted latency
bounded by Invariant H. Under a *disclosed* reservation it is worse — the customer has been told someone
is considering their booking who provably is not.

**Ruling: `TakeOrder.Handler`, after a successful assignment, sets `PreferredHoldUntilUtc = now` on any
order that (a) names the caller as beneficiary, (b) still has a live reservation, and (c) overlaps the
window they just committed to** — then runs §D6's shared notifier. This is ADR-0036 D2 consequence #4
executed verbatim (*"a cleaner-side 'pass on this' action is `PreferredHoldUntilUtc = now` — one write,
no new column, no new state"*), and the query is a **fourth terminal shape over the existing predicate**,
not a new one:

```csharp
LiveCommitmentsInWindow(orders, start, end)
    .Where(o => o.PreferredEmployeeId == employeeId && o.PreferredHoldUntilUtc > nowUtc)
```

ADR-0039 D3.2 explicitly permits N terminal shapes over one shared window filter; it forbids a second
*predicate*. This is the former.

### D2 — The reservation is **NOT an `OrderEmployee` row**, and this is the load-bearing decision

> **The pending state stays exactly where ADR-0036 put it — the `(PreferredEmployeeId,
> PreferredHoldUntilUtc)` pair on `Order`. No assignment row is created until the cleaner confirms.**

The obvious implementation of *"he has to be assigned"* is to write the `OrderEmployee` row at booking
and give it a confirmation state. **Rejected, on four grounds, three of which are shipped code:**

1. **It spends the cleaner's weekly cap on a job they never agreed to.**
   `OrderRepository.GetEmployeeOrderCountThisWeekAsync` (`:245-257`) counts orders where
   `AssignedEmployees.Any(e => e.EmployeeId == employeeId)` in the current UTC week, **with no status
   term and no confirmation term**. It is the input to `TakeOrder`'s rating-tiered 3/6/10 limit
   (`TakeOrder.cs:200-207`). Three unanswered pending offers in a morning would exhaust a 3-cap
   cleaner's whole week.
2. **It blocks the cleaner's calendar against jobs they would have taken.** A row makes the order match
   `LiveCommitmentsInWindow(...).Where(o => o.AssignedEmployees.Any(...))` (`:282-284`) — the very
   predicate `TakeOrder`'s conflict gate and ADR-0039's picker read. The cleaner is marked busy for a
   window they have not accepted.
3. **It changes what the customer pays to cancel, and burns a membership benefit.**
   `CancellationAssessor.cs:55` is literally `var hasBeenAccepted = order.AssignedEmployees.Count > 0;`
   — and that boolean drives (a) `BookingPolicy.ClassifyCancellation`'s `FreeNotAccepted` arm
   (`:252-255`) and (b) the express-waiver release at `CancelOrder.cs:143-146`. **A row at creation
   makes every favourite-cleaner booking fee-bearing and waiver-consuming from the instant it is
   created, before any human agreed to anything.** That is a money defect created by a display feature.
4. **Undoing (1)–(3) means teaching the occupancy predicate a confirmation term** — a second definition
   of "occupied", which ADR-0039 D3.2 rejects outright and which fails in the direction that
   double-books a cleaner standing in someone's flat.

**The seam this protects, stated so it survives the next feature:**

> **A customer's preference may spend the platform's fill window. It may never spend a cleaner's
> capacity.** Capacity — the weekly cap and the calendar — is consumed only by a commitment the cleaner
> made. Everything a customer can do alone is bounded by Invariant H.

**What the customer's choice therefore does and does not do, per axis:**

| | During the reservation |
|---|---|
| Order's seats visible to other cleaners | **withheld** (`OrderVisibility.NotHeldFrom`, unchanged) |
| Beneficiary's weekly cap | **untouched** |
| Beneficiary's calendar / overlap answer | **untouched** |
| `Order.AssignedEmployees` | **empty** |
| `CancellationAssessor.hasBeenAccepted` | **false** — cancelling is free and the express waiver is released |
| `Order.CurrentStatus` | **unchanged** (`New` for cash; `New → Confirmed` on the card webhook, as today) |
| `OrderEmployeePay` | does not exist yet, as today |

### D3 — The four things the reservation gains, and nothing else

| # | Gains | Cost |
|---|---|---|
| **a. A name** | a derived customer-facing state (§D7) and a cleaner-facing surface that is *not* the open board (§D9) | one query, one DTO block |
| **b. A decline** | `DeclinePreferredOffer` — **one write**: `PreferredHoldUntilUtc = now` | one command, no column |
| **c. Customer-visible state** | the state + the respond-by instant on the customer's order detail | ⚠️ `nswag-regen` |
| **d. A customer-facing exit** | one message at lapse, and a **capped** re-offer (§D5) | one sweep, two columns |

**The confirm is `TakeOrder`, unchanged.** Not a new command, not a widened one, not a bypass. The
button says "Confirm" instead of "Take"; the request is identical. This is what keeps
`TakeOrder.Validator`'s one ordered `Cascade.Stop` chain (`:46-71`) the single write gate — a
confirmation path that skipped approval, profile, cap or conflict would be a second, weaker take, and
ADR-0037 D6 spent a whole panel round establishing that a second chain in that validator breaks the
first.

### D4 — How long, and who observes the end

**The window is `BookingPolicy.ComputePreferredHold`, unchanged** — `min(lead × 0.10, 12 h)`, zero below
`2 × StandardLeadTimeHours` (8 h). The formula, the fraction, the ceiling and the floor are all
owner-settled or panel-settled and are **not reopened**. What changes is only that the deadline is now
**disclosed**, to both sides.

Derived from the shipped constants, so a reviewer can check by arithmetic:

| Lead at creation | Reservation | What the customer is told |
|---|---|---|
| 2–8 h | **none** (`ShortLeadTime`: notify only, D4.1) | **nothing about a reservation** — there is none. §D7. |
| 8 h | **48 min** (the shortest the formula can produce) | "awaiting confirmation until HH:MM" |
| 24 h | 2 h 24 | " |
| ≥ 120 h | **12 h** (ceiling) | " |

**Who observes the end — and this is where ADR-0036 D2 is refined rather than broken:**

| Question | Answer | Actor |
|---|---|---|
| When do the seats re-open to the board? | at `PreferredHoldUntilUtc` | **none** — `OrderVisibility.NotHeldFrom` term 3, a `WHERE` clause |
| When does the cleaner stop seeing it as theirs? | same instant | **none** — the same term |
| When is the customer told? | within one sweep interval after | **a timer** (§D6) |

**The release and the announcement are separated on purpose.** ADR-0036 chose the actorless expiry
because *"the failure mode of a job-driven expiry is an order stuck held — the exact catastrophic
outcome."* That reasoning is not weakened here, it is honoured: **no code path in this design can leave
an order reserved.** The sweep's worst failure is a customer who is not prompted, on an order that is
already back on the open board.

### D5 — The refusal loop, and its termination

The owner named two options. Both are ruled here.

#### D5.1 — *"select another employee that will go through the same flow of approval"*

A new **customer** command, `ChoosePreferredCleaner(orderId, employeeId)`, on the customer + customer
mobile hosts. It is the same decision as at booking and therefore **calls the same resolver**:

```csharp
// Cleansia.Core.AppServices/Features/Orders/ChoosePreferredCleaner.cs
// Re-runs IPreferredCleanerHoldResolver against the EXISTING order. It re-implements no gate:
// membership, cleaner eligibility, work country, reachability, the ADR-0039 slot check and the
// lead-time floor are all its answer, evaluated against the order's own CleaningDateTime and
// EstimatedTime (which are already persisted — no client input decides a server answer, S1).
public record Command(string OrderId, string EmployeeId) : ICommand<Response>;
```

**"the same flow of approval" is satisfied by construction** — there is one resolver and it is the one
`OrderFactory` calls. And on success the aggregate re-grants:

```csharp
// Order.GrantPreferredHold — WIDENED from "set once, at creation" to re-callable, and the
// aggregate keeps the structural invariants rather than the policy ones:
//   - beneficiary non-empty                (unchanged, ADR-0036 D2)
//   - AssignedEmployees.Count == 0         (a taken order has no reservation to grant)
//   - untilUtc > the current value          (a re-grant may never SHORTEN a live reservation, which
//                                            would be a way to evict a beneficiary silently)
//   - PreferredOfferRound < BookingPolicy.MaxPreferredOfferRounds
```

`CreateOrder.Validator`'s eligibility rule (`:168-170`,
`OrderRepository.UserHasCompletedOrderWithEmployeeAsync`) and its Plus gate (`:166-167`) are mirrored in
this command's validator as **the same two `MustAsync` calls in the same order** and with the same two
error keys — entitlement first, because it reveals least (`CreateOrder.cs:158-161`).

**Two structural refusals, and both are silent about people:** the command refuses the **same**
`EmployeeId` the row already carries (that is the person who just lapsed or declined — §D6 keeps the id
on the row precisely so this is possible without telling the customer anything), and it refuses once any
cleaner is assigned. All refusals collapse to **one** new key, `order.preferred_offer_closed`, per
ADR-0036 D5.2's rule (*never introduce an error key that names the exclusivity; reuse the most generic
refusal the caller could already have received*). Five locales × the three customer apps.

#### D5.2 — *"or suggest a random cleaner"* is **the open board, named** — and is not built

> **Ruling: "any cleaner" means the order is released to the board immediately. `PreferredHoldUntilUtc
> = now`, one write, no new mechanism, no new column, no engine.**

At lapse the order is *already* on the open board; the "random cleaner" choice is therefore the
**default**, and choosing it explicitly only means "don't ask me again" (it stamps the round counter to
the cap). The mechanism is Cleansia's shipped pull dispatch: the first eligible cleaner takes it.

**The alternative — the platform picks a specific substitute at random and offers them exclusively — is
rejected (A5).** It would withhold the order from every *other* eligible cleaner in favour of one chosen
by dice, which strictly *slows* the fill; and it would create a second dispatch model beside the pull
board, for a customer who has just told us they no longer care who comes.

⚠️ **What the customer is *shown* for this option is a product decision and is escalated, not
defaulted** (`Q-ASSIGN-01`). Showing a **named** substitute would be a promise the platform cannot keep
— that person may never take the job — and manufacturing that promise is precisely the defect
`Q-PROMISE-02` was raised about in the first place.

#### D5.3 — Termination: a **count**, because the window formula does not terminate

The formula recomputes off the *current* lead time, so each round is ~90% of the previous one below the
ceiling. **That decays but does not stop.** From a seven-day booking: 168 h → 12 h (ceiling), then 156,
144, 132, 120 h all still hit the ceiling, and from 120 h the sequence is `lead × 0.9` per round, so
reaching the 8-hour floor takes `log(8/120) / log(0.9) ≈ 26` further rounds — **on the order of thirty
reservations on one booking.** A lead-time floor is not a loop bound.

```csharp
// BookingPolicy — platform-wide, per ADR-0035 D2.1's placement rule.
/// Total preferred-cleaner reservations one order may ever carry: the booking's own choice, plus
/// exactly one re-offer. The number is DERIVED, not chosen: MaxPreferredOfferRounds *
/// PreferredHoldFraction is the share of a seat's fill window this feature may consume, and
/// PreferredOfferInvariantTests pins it at <= 1 - MinimumOpenBoardShare. Raising it requires
/// lowering the fraction or re-ruling the invariant; neither number moves alone.
public const int     MaxPreferredOfferRounds = 2;
public const decimal MinimumOpenBoardShare   = 0.80m;   // was 0.90m under ADR-0036 D3
```

**Invariant H, restated:**

> **For every SEAT on every order, at least 80% of that seat's fill window is open to the entire board.**
> Two rounds × a tenth each. The ceiling still binds each round independently, so the absolute worst
> case is 24 hours of a fill window that is at least 120 hours long.

**What happens when the customer keeps choosing people who decline** is therefore: they get exactly one
second choice, and after it the order is on the open board and the platform stops asking. **Doing
nothing is always a complete answer** — the order is on the board the instant a reservation lapses, so
a customer who never opens the app loses nothing but the perk.

### D6 — The sweep **announces**; it never releases

```csharp
// Cleansia.Core.AppServices/Features/Orders/NotifyLapsedPreferredOffers.cs
// Timer: "0 */5 * * * *" — the cadence precedent is FiscalReconciliationFunction.cs:16 and
// RetryFailedFiscalRegistrationsFunction.cs:12. FIVE minutes and not fifteen because the SHORTEST
// reservation the policy can produce is 48 minutes (8 h floor x 0.10), and a 15-minute sweep would
// add up to 31% to it before the customer hears anything.
public record Command(int BatchSize = 200) : ICommand<Response>;
```

**Its predicate — four terms, all on columns that already exist plus one new receipt:**

```
PreferredHoldUntilUtc != null
  AND PreferredHoldUntilUtc <= nowUtc            -- the reservation is over (the CLOCK ended it, not us)
  AND AssignedEmployees.Count == 0               -- nobody took it: this is the "nobody came" case
  AND PreferredOfferLapseNotifiedAt == null      -- idempotency
  AND CurrentStatus is not (Completed | Cancelled)
  AND RecurringTemplateId == null                -- D6.2
```

**Its only write is `Order.PreferredOfferLapseNotifiedAt = nowUtc`** plus the customer notification.
It does **not** touch `PreferredHoldUntilUtc`, does **not** touch `PreferredEmployeeId`, does **not**
append a status track, does **not** assign or unassign anyone.

#### D6.1 — It must **not** call `ClearPreferredHold()`, and the reason is in another sweep

The tidy-looking implementation — clear the pair after notifying, so the predicate is self-idempotent
with **zero new columns** — is **wrong, and would silently re-open a defect ADR-0036 spent a panel round
closing.** `NewJobsDigestService.ApplyFreshness` (`:261-275`) reads the pair to decide that a lapsed
order is *new again* to every other cleaner:

```csharp
|| (o.PreferredEmployeeId != null
    && o.PreferredEmployeeId != employeeId
    && o.PreferredHoldUntilUtc > since
    && o.PreferredHoldUntilUtc <= sweepStartedAtUtc)
```

A 5-minute sweep that nulls the pair would erase that disjunct before the 30-minute digest ever sees it,
and **the order would fall out of the notification channel permanently — board-only, findable solely by
someone who happens to scroll.** That is ADR-0036 Fact B, restored through a back door. `Order.cs:689`'s
`ClearPreferredHold()` in `AnonymizeCustomerData` stays the only production caller.

⇒ **A receipt column is required. `Order.PreferredOfferLapseNotifiedAt`, nullable, `null` for every
existing row, precedent `Order.RecurringReminderSentAt` (`Order.cs:275-281` — a column that exists for
exactly this, so a reminder sweep does not push twice).** No backfill, no index.

#### D6.2 — Recurring occurrences get the reservation but **not** the prompt

`MaterializeRecurringBookingTemplate.cs:240` carries the template's preference into every occurrence, 7
days ahead. Un-suppressed, a weekly template whose favourite never answers produces **one customer push
per week, forever**. The `RecurringTemplateId == null` term is the exact shape
`CleanupStalePendingOrders.cs:70` already uses for the same reason, and it follows the living doc's
ruling *"reject where someone can react; degrade where nobody can"*: the customer did not initiate this
booking and cannot usefully be asked about it at 03:00. **The reservation, the push to the cleaner and
the customer-visible state on the order all still happen** — only the interruption is withheld. Flip
condition: a per-template preference surface, filed.

#### D6.3 — Tenancy

The sweep runs with no JWT. `GetQueryableIgnoringTenant()`, then `GroupBy(o => o.TenantId ?? "")`,
`ClearTenantOverride()` / `SetTenantOverride(key)` per group, and **`CommitAsync` inside the loop** —
`CleanupStalePendingOrders.cs:67-119` is the reference shape and this sweep is the easy case, because
the notification recipient is the order's own customer, so the group key *is* the recipient's tenant.
A tenant-scoped repository call inside this sweep returns nothing.

#### D6.4 — The decline path shares the notifier, so the two cannot drift

`DeclinePreferredOffer` must tell the customer **immediately**, not in ≤5 minutes. So the decline and the
sweep call one shared notifier — the shape `OrderAssignmentCancellationNotifier` already establishes
(`CancelOrder.cs:151`):

```csharp
// One producer of the customer-facing lapse signal, two callers (the sweep, the decline), so the
// message, the args and the receipt stamp cannot diverge. Stamps PreferredOfferLapseNotifiedAt,
// which is what makes the sweep skip an order the decline already announced.
static Task NotifyPreferredOfferClosedAsync(Order order, INotificationProducer producer, DateTime nowUtc, CancellationToken ct);
```

**`DeclinePreferredOffer` writes `PreferredHoldUntilUtc = now` and nothing else structural.** Its
existence gate is `TakeOrder`'s (`TakeOrder.cs:83-91`) — the same
`GetQueryable().Where(OrderVisibility.NotHeldFrom(employeeId, now)).AnyAsync(o => o.Id == …)` — refusing
with the existing `BusinessErrorMessage.OrderNotFound`, so a non-beneficiary cannot distinguish
*"someone else's reservation"* from *"no such order"* (ADR-0036 D5.2). Idempotent: a second decline
finds the reservation already expired and returns success.

### D7 — What the customer sees, and what they are never told

#### D7.1 — The state is **derived, never stored**

```csharp
// Cleansia.Core.Domain/Orders/PreferredOffer.cs — PURE. Four inputs, all already on the row.
public enum PreferredOfferState { None = 0, AwaitingConfirmation = 1, Accepted = 2, Closed = 3 }

public static PreferredOfferState StateOf(
    string? preferredEmployeeId, DateTime? holdUntilUtc,
    bool beneficiaryIsAssigned, DateTime nowUtc);
```

Derived, for the same reason ADR-0036 stores a deadline rather than a flag: **a derived state has no
writer, cannot go stale, needs no backfill and cannot be left inconsistent by a path nobody remembered.**
`None` covers every case with no reservation — no preference, a non-member, a declined resolve outcome,
and **the entire 2–8 h notify-only band**, which is correct: in that band nothing is withheld and telling
the customer someone is "considering" their booking would be false.

#### D7.2 — On the customer's order detail (⚠️ `nswag-regen`)

```
preferredOffer: {
  state:          PreferredOfferState      // derived above
  cleanerName:    string?                  // the person the customer themselves picked
  respondByUtc:   DateTime?                // the deadline; null unless AwaitingConfirmation
  canChooseAnother: bool                   // PreferredOfferRound < MaxPreferredOfferRounds && no assignment
}
```

**A nested optional block, so a client that has not been rebuilt is unaffected.** ⚠️ **Both mobile
customer clients carry the row-dropping mapper idiom** ADR-0039 flagged (`OrderApi.kt` `toAppDto`'s
`?: return null` chain; `ServingCleanersClient.swift`'s `compactMap`/`guard let` into a non-optional
struct). An absent `preferredOffer` must **not** drop the order row. That is an AC on the mobile ticket,
pinned by an automated test per client, not by review.

**`respondByUtc` is an instant, not a countdown.** A countdown is a client concern and an anxiety
machine; an instant is a fact, and it is the fact the customer needs because §D6's prompt arrives at
roughly that time. Rendering it as relative time is the client's choice.

#### D7.3 — The customer is **never** told which way the offer ended

> **One sentence covers both a decline and a silence. The customer learns that the offer ended, and is
> offered a second choice. They are never told that a specific person refused, and never told that a
> specific person did not answer.**

This adopts ADR-0039 D7's already-ruled neutral-line rule rather than inventing one: *a sentence about
**what Cleansia can offer** stays true when the predicate later widens; a sentence about **what the
person did** becomes a lie the moment it does.* Three further grounds:

1. *"Anna declined"* and *"Anna didn't answer"* are both statements about a worker's conduct disclosed to
   a third party for that third party's convenience. **`Q-AVAIL-04` — which lawful basis covers that —
   is open and was re-scoped by the ADR-0039 panel from notice to basis.** Shipping the strongest form
   of the disclosure while the weakest form's basis is unresolved is the wrong order.
2. **It destroys the perk's own supply loop.** A customer who learns their favourite refused them stops
   choosing that favourite — and `GetMyServingCleaners`' set accumulates one paid cleaning at a time
   (`GetMyServingCleaners.cs:64-88`), so the platform cannot replace what it burns.
3. **The one thing the disclosure would buy is bought mechanically instead.** The argument for telling
   the customer is that otherwise they might re-offer the same person and lose a second window. §D5.1's
   command **refuses the same `EmployeeId`** and ADR-0039's picker already greys out anyone busy in the
   slot. The customer is prevented from the mistake rather than told about a person.

**The cleaner side is unchanged from ADR-0036 D4 and is not reopened:** `PreferredEmployeeId` never
appears on a partner-facing DTO, no surface ever says an order is held for someone else, no cleaner ever
learns they were passed over, and a decline notifies nobody.

### D8 — Money and schedule while a reservation is live

**Nothing changes, and that is the deliberate result of D2.**

| Axis | During a live reservation |
|---|---|
| **Fulfilment** (`Order.CurrentStatus`) | `New` for cash; `New → Confirmed` when the card webhook lands (`HandlePaymentNotification`). **No new status is introduced and none may be** — ADR-0037 D5 forbids a second source of truth for a state already tracked, and `OrderStatus.Pending` stays dead. |
| **Payment** | untouched. `OrderPaymentDispatcher` runs at creation exactly as today. |
| **The two retractors** | untouched. `CleanupStalePendingOrders` (15-min, `PaymentStatus == Pending ∧ PaymentType == Card ∧ RecurringTemplateId == null`) and `AutoCancelStaleRecurringOrders` (hourly) both keep working, and both can cancel an order that carries a live reservation. **Correct:** a reservation is not a payment. |
| **Cancellation fee** | `CancellationAssessor.cs:55` still reads `AssignedEmployees.Count > 0` and is still **false**. Cancelling during a reservation is free (`FreeNotAccepted`, `BookingPolicy.cs:252-255`). |
| **Express waiver** | still released on that cancel (`CancelOrder.cs:143-146`), because it keys on the same boolean. |
| **Pay** | untouched — `OrderEmployeePay` is created downstream of completion; no row, no pay. |
| **Fiscal** | untouched. Nothing here touches receipt registration or the enforcement modes. |

**The writer census: which shipped writers this ADR affects.** `TakeOrder.Handler` (one added write,
D1.1) and **nothing else**. `AdminReassignOrder` (`:86`, `:98` — the only production caller of
`UnassignEmployee` and the only non-take assigner) is untouched: an admin assignment during a live
reservation already consumes it through `OrderVisibility` term 5 (`AssignedEmployees.Any()`), which
ADR-0036 D5 widened for exactly this case.

### D9 — Composition with offerability: **ADR-0037 is not extended, because it already answers**

The new cleaner-side surface — *"jobs waiting for your answer"* — is **four existing conjuncts and one
equality**, in the order the digest already uses (`NewJobsDigestService.cs:131-138`):

```csharp
// GetMyPendingOffers — partner + partner-mobile. No new predicate exists anywhere in this ADR.
orders
  .Where(o => o.PreferredEmployeeId == employeeId)          // the equality
  .Where(o => o.PreferredHoldUntilUtc > nowUtc)             // the reservation is live
  .Where(o => o.AssignedEmployees.Count < o.MaxEmployees)   // seat arithmetic, as everywhere
  .Where(OrderAvailability.IsOfferableSql)                  // ADR-0037, conjoined — NOT extended
```

**`OrderAvailability` is untouched and must stay untouched.** It answers *"is this order live work
someone may take"* — a property of the order alone, four columns in, a bool out. *"Is it reserved for
me right now"* is `OrderVisibility` — a property of the (order, cleaner) pair. They are separate
conjuncts on the same surfaces, which is the shipped shape. **If this design had needed a new arm in
`OrderAvailability`, that would have been the signal the design was wrong.**

⚠️ **One defect this surface must not inherit.** `OrderAccessService.CanBrowseOrderAsync` (`:88-91`)
conjoins `HasAvailableSpots` and `NotHeldFrom` but **not** `IsOfferableSql`. So a beneficiary can open
the detail of a `New` + **Card** order whose money has not landed — an order `TakeOrder.cs:56-57` will
refuse with `order.not_takeable`, and which `CleanupStalePendingOrders` may cancel within ~1 h 15 m.
Under a *disclosed* reservation that becomes "you were assigned a job that vanished". **The pending-
offers list carries the conjunct from day one**; whether `CanBrowseOrderAsync` should also carry it is a
pre-existing question and is **filed, not decided here**.

### D10 — Notifications: **zero new partner events**, one new customer event

| Direction | Event | New? |
|---|---|---|
| → cleaner, at booking | `order.preferred_offer` | **no** — shipped (`OrderFactory.cs:194`) |
| → cleaner, at lapse | *(none)* | **deliberately none.** "You missed a job" is a negative-reinforcement push on a channel the cleaner can mute wholesale. |
| → cleaner, reminder before the deadline | *(none)* | **out of scope, with a flip condition** (A9): a measured non-response rate on ceiling-length windows. Not invented without evidence. |
| → **customer**, at lapse or decline | **`order.preferred_offer_closed`** — args `orderId`, `orderNumber` | **yes, one.** Category: a new `NotificationCategory` member, or the existing `OrderUpdates`. Ruled: **`OrderUpdates`** — it is an update about the customer's own order and must not need its own opt-out to be discoverable. |

**Does the 30-minute digest still make sense beside this?** Yes, and the two do not compete. The digest
emits a **count** of open board work and stamps `Employee.LastNewJobsDigestAt`; the targeted offer is
**one named order** and deliberately does neither (`NotificationEventCatalog.cs:32-44`). That separation
is what lets the confirmation window be set by the customer's tolerance for latency rather than by our
sweep interval — the exact property ADR-0036 D4 bought — and this ADR relies on it rather than
disturbing it. The **only** cadence question this raises is the new customer-side sweep's, answered in
D6 at 5 minutes with the 48-minute floor as the derivation.

### D11 — Per-country variation: the seam is named, and nothing is branched

`CountryConfiguration` today carries currency, language, date/time format, phone prefix, VAT rates, tax
and registration identifiers, the default payment gateway, a legal-requirements JSON, the fiscal
enforcement mode, the payout scheme and two Stripe refund-fee figures (`CountryConfiguration.cs:11-96`).
**It carries no scheduling number at all.** Adding the first one is a decision in its own right.

**Ruling: `MaxPreferredOfferRounds`, `MinimumOpenBoardShare`, the fraction, the ceiling and the floor
stay platform-wide `const`s on `BookingPolicy`**, per ADR-0035 D2.1's placement rule. The flip is
already cheap and already recorded by ADR-0036 D2 consequence #4: making the window per-country is *"a
change to the computation, with no schema change and no effect on live orders"* — one column read into
one pure function's inputs.

**And the standing constraint, restated with its evidence:** no handler may branch on a country code.
`PreferredCleanerHoldResolver.cs:65-69` takes `serviceCountryId` only to **compare** it with the
cleaner's `WorkCountryId`; it never switches on a value. Any per-country behaviour added later reads a
`CountryConfiguration` column.

### D12 — What must be checked before this ships, on the cleaner's side and the customer's side

The owner asked for *"the functionality around it for both employee and customer"* to be checked. What
the census found, each either handled above or filed:

| Finding | Where |
|---|---|
| An `OrderEmployee` row would make cancelling fee-bearing and burn the express waiver | **handled** — D2, no row |
| An `OrderEmployee` row would spend the cleaner's weekly cap and calendar | **handled** — D2 |
| A sweep that clears the pair kills the digest's hold-expiry freshness source | **handled** — D6.1 |
| Recurring would emit one customer push per occurrence, forever | **handled** — D6.2 |
| A beneficiary who takes a conflicting job leaves a live reservation nobody can honour | **handled** — D1.1 |
| `CanBrowseOrderAsync` omits the offerability conjunct, so a beneficiary can open a `New`+Card order that may be cancelled under them | **filed, not fixed** — D9 |
| The web customer wizard has **no preferred-cleaner picker at all** (`order-wizard.facade.ts` sends `undefined`), so a web customer cannot use the perk or the re-offer | **filed** — pre-existing, called out because the customer-side exit is meaningless without it |
| The perk is effectively **mobile-cleaner-only** until the partner web SPA registers push devices (ADR-0036 D4.1's reachability gate) — so a customer's favourite who works from the web board can never be reserved | **pre-existing, restated** — under "assigned" it is a bigger product fact than it was under a silent hold |

---

## Alternatives considered and rejected

| # | Alternative | Why not |
|---|---|---|
| **A1** | **A real `OrderEmployee` row at booking, with a confirmation state on the row** | The obvious reading of *"assigned"*. Rejected on three pieces of shipped code: `GetEmployeeOrderCountThisWeekAsync` (`:245-257`) has no status term, so the row spends the cleaner's weekly cap; `LiveCommitmentsInWindow` + `:283` makes the row block their calendar; and `CancellationAssessor.cs:55` makes the customer's cancellation fee-bearing and burns their express waiver (`CancelOrder.cs:143-146`) from the instant of creation. Undoing any of it requires a confirmation term inside the occupancy predicate — a **second definition of "occupied"**, which ADR-0039 D3.2 rejects and which fails in the double-booking direction. |
| **A2** | **A new `OrderStatus` (`AwaitingCleanerConfirmation`)** | The fulfilment axis is a property of the *work*. A multi-seat order can have one confirmed and one pending cleaner and one order-level status cannot express that. It also puts a new integer on the wire to three generated clients, forces an arm into `OrderAvailability` and therefore into ten surfaces, and is exactly the "second source of truth for one fact" ADR-0037 D5 forbids. |
| **A3** | **A sweep that releases the reservation** (deletes/flips it, then notifies) | Reintroduces the failure mode ADR-0036 D2 was written to eliminate: *an order stuck reserved*, with no actor permitted to clear it if the timer is down. Splitting release (clock) from announcement (sweep) costs one column and keeps the catastrophic failure unreachable. |
| **A4** | **Read-time expiry with the state on the assignment row** — every seat count learns a clock | Threads `now` through `Order.AvailableSpots`/`HasAvailableSpots` (`:136-137`), `OrderSpecification.cs:141` **and** `:163`, `NewJobsDigestService.cs:135`, and `OrderMappers.cs:94`. Five seat-count surfaces, each of which can be forgotten independently — the sprawl ADR-0036 D5 and ADR-0037 D0 exist to prevent. |
| **A5** | **The platform picks a random substitute and offers them exclusively** | Withholds the order from every other eligible cleaner in favour of one chosen by dice, which strictly slows the fill, and manufactures a second dispatch model beside the pull board. *"Random cleaner"* is the board, named (D5.2). |
| **A6** | **Automatic cascade to the next serving cleaner, without asking the customer** | Rejected on the owner's own words — *"offer customer either select another employee… or suggest a random cleaner"* — and because each automatic round spends fill window with no human deciding it is worth spending. |
| **A7** | **Tell the customer the cleaner declined (or did not answer)** | ADR-0039 D7's neutral-line rule, adopted: a sentence about what Cleansia can offer stays true when the predicate widens; a sentence about what a person did becomes a lie. Plus `Q-AVAIL-04` (lawful basis for disclosing a worker's state to a third party) is **open**, and the one benefit it buys is bought mechanically by D5.1's same-id refusal. |
| **A8** | **A `PreferredOffer` child table** (an audit row per offer) | Richer — it would support "never re-offer anyone who already declined" across rounds and an offer audit — but it is a table, a migration and a new aggregate member to carry a counter that one `int` carries. **Recorded as the durable answer with its flip condition:** a product need for cross-round exclusion, or an audit requirement on offers. |
| **A9** | **A reminder push before the deadline** | Doubles volume on a category cleaners can mute wholesale, on no evidence. Flip condition: a measured non-response rate on ceiling-length (12 h) windows. |
| **A10** | **Keep the hold and fix only the copy** (`Q-PROMISE-02` option (a)) | The owner chose option (b) explicitly. Off the table. |
| **A11** | **Put the window on `CountryConfiguration` now** | It carries no scheduling number today (`:11-96`); the first one is its own decision, and ADR-0036 already priced the later flip at "a change to the computation, no schema change". |
| **A12** | **One column instead of two** — reuse `PreferredOfferLapseNotifiedAt` as the round counter | Collapses two facts with two writers (the customer's action; the sweep's receipt) into one, which makes *"declined, customer not yet told"* inexpressible — the exact collapse ADR-0036 D2's two-column ruling exists to prevent. |
| **A13** | **A dedicated `ConfirmPreferredOffer` command** instead of reusing `TakeOrder` | A second write path into assignment, which would either duplicate `TakeOrder.Validator`'s ordered chain (approval · profile · cap · conflict) or be weaker than it. ADR-0037 D6 already established that a second chain in that validator breaks the first. The confirm is a **label**, not a command. |

---

## How a reviewer verifies compliance

1. **`git grep -n "OrderEmployee.Create"`** returns exactly the two production call sites it returns
   today (`TakeOrder.cs:265`, `AdminReassignOrder.cs:98`). **A third is a rejection of D2.**
2. **`OrderAvailability.cs` is byte-unchanged.** Any diff to it means D9 was not understood.
3. **`OrderVisibility.cs` is byte-unchanged.** The reservation mechanism is not being reimplemented.
4. **The lapse sweep's writes.** Read the handler: the only `Order` property it assigns is
   `PreferredOfferLapseNotifiedAt`. Any assignment to `PreferredHoldUntilUtc`, `PreferredEmployeeId`,
   any `AddOrderStatus`, or any call to `ClearPreferredHold` fails D6/D6.1.
5. **The sweep's tenancy**, against `CleanupStalePendingOrders.cs:67-119`: `GetQueryableIgnoringTenant`
   → group → `ClearTenantOverride` → `SetTenantOverride` → **`CommitAsync` inside the loop**. A commit
   after the loop stamps every group with the last tenant.
6. **`ChoosePreferredCleaner.Validator` calls `IPreferredCleanerHoldResolver`** and contains **no**
   membership / country / approval / device / slot logic of its own. Any re-implemented gate fails D5.1.
7. **`OrderVisibility.NotHeldFrom` is the only existence gate on `DeclinePreferredOffer`**, and its
   failure message is `BusinessErrorMessage.OrderNotFound` — no new key names the exclusivity
   (ADR-0036 D5.2).
8. **`PreferredOfferInvariantTests`** asserts
   `MaxPreferredOfferRounds * PreferredHoldFraction <= 1m - MinimumOpenBoardShare`. This is the whole of
   Invariant H's enforcement and neither number may move alone — same idiom as `OrderSpanCapTests`'
   `cap <= floor`.
9. **The digest still sees lapsed orders.** A test that creates an order with an expired reservation and
   an un-run/already-run sweep, and asserts `NewJobsDigestService` still counts it for a third cleaner.
   This is the D6.1 regression guard and it is the one most likely to be lost.
10. **`grep -n "preferredOffer" ` in both mobile customer mappers**: the field's absence must produce a
    row with `state == none`, never a dropped order. One automated test per client.
11. **No country code appears in any new handler or validator.** `grep -niE '"(cz|sk|ua|de|pl|us)"'`
    over the new files returns nothing.

---

## Consequences

**Positive**
- The owner's five asks are delivered with **two new columns, one timer, three commands, one query and
  zero changes to any of the three shipped rules** (`OrderAvailability`, `OrderVisibility`,
  `LiveCommitmentsInWindow`).
- The customer's promise becomes true in all five locales at once: `cs`/`sk`/`ru`'s *"will be
  preferentially assigned"* is now backed, and `en`/`uk`'s *"prioritized"* is the understatement the
  owner named. **T-0544 / T-0491's copy work gains its ruling** and the affirmative wave is now owed.
- A cleaner can say no in one tap, which is the first supply-side control this feature has ever had.

**Negative, and priced**
- **Invariant H drops from 90% to 80%.** Two reservations on one booking. Escalated (`Q-ASSIGN-02`).
- **There is now an actor in this feature.** ADR-0036 had none. The failure modes do not merge (D6/AC2),
  but the operational surface grew by one timer and one receipt column.
- **⚠️ Two owner-only manual steps**: an `ef-migration` (two additive columns, no backfill, no index) and
  an `nswag-regen` (one nested optional DTO block + two customer endpoints + two partner endpoints).
- **Six clients change**: three customer apps (state block, re-offer, one error key × 5 locales) and
  three partner apps (a pending-offers surface + a decline). The web customer app cannot use any of it
  until it gets a picker at all — filed.
- **One new customer notification event** × 5 locales × 3 customer apps.

**Tickets this ADR asks the PM to file** *(sizes are relative; none is estimated in hours)*

| # | Carries | Depends on |
|---|---|---|
| **R1** | The two `Order` columns + the widened `GrantPreferredHold` + `PreferredOffer.StateOf` + `PreferredOfferInvariantTests`. ⚠️ `ef-migration` | — |
| **R2** | `DeclinePreferredOffer` + the shared `NotifyPreferredOfferClosedAsync` + D1.1's implicit decline in `TakeOrder.Handler` | R1 |
| **R3** | `NotifyLapsedPreferredOffers` + the timer + the tenancy shape + **the D6.1 digest regression test** | R1, R2 |
| **R4** | `ChoosePreferredCleaner` (customer) + the one error key × 5 locales | R1 |
| **R5** | `GetMyPendingOffers` (partner + partner-mobile) with the D9 conjunct set | R1 |
| **R6** | The customer order-detail DTO block. ⚠️ `nswag-regen` | R1, R4 |
| **R7** | The three partner clients: the pending-offers surface + Confirm/Decline. **"Confirm" calls `TakeOrder`** | R5 + regen |
| **R8** | The three customer clients: state, respond-by, "choose another / any cleaner". ⚠️ **the row-dropping mapper pin per client** | R6 + regen |
| **R9** | The affirmative copy wave — five locales, all clients — now that the promise is decided. Feeds T-0491 / T-0544 | R1 |
| **F1** *(filed, not built)* | `CanBrowseOrderAsync` omits the offerability conjunct (D9) | — |
| **F2** *(filed, not built)* | The web customer wizard has no preferred-cleaner picker at all | — |
| **F3** *(filed, not built)* | A per-template preferred-cleaner surface, which is what would let D6.2's suppression be lifted | — |

**Catalog edits this ADR carries at acceptance** *(not applied while `proposed`)*
- `patterns-backend.md` §"Bounded exclusivity on a pull board" — a new bullet:
  > **If the exclusivity must be *disclosed*, split RELEASE from ANNOUNCEMENT.** Keep the release a
  > clock comparison with no actor; give the timer only the announcement and one receipt column it
  > alone writes. A sweep that also releases restores the *stuck-exclusive* failure the deadline was
  > chosen to eliminate — and a sweep that **clears the pair** can silently delete a *different*
  > sweep's freshness source. Check who else reads the pair before you tidy it up.
  And a second:
  > **A reservation must never consume the beneficiary's capacity.** Cap counters and calendar-overlap
  > predicates key on commitments, not offers. If representing an offer requires teaching either of
  > them a new term, the representation is wrong.
- `agents/knowledge/roles/preferred-cleaner-hold-resolver.md` — the resolver's "does NOT know" list
  gains: *the round count, the notification receipt, and whether anyone has been told* (it stays a pure
  read; the aggregate owns the counter and the notifier owns the receipt).
- A new role card, `roles/preferred-offer-lapse-notifier.md`.
- `agents/architecture/decisions/preferred-cleaner-dispatch.md` — updated in the same change as
  acceptance, including the correction that ADR-0036/0039 **are** shipped (the page still says
  "Nothing is shipped yet").

---

## Escalations — decisions I deliberately did not default

**`Q-ASSIGN-01` (blocking: no — but it blocks R8's copy) — for *"suggest a random cleaner"*, does the
customer see a NAME, or "the first available cleaner"?**
The **mechanism** is ruled (D5.2: the open board — the platform does not pick a substitute). What is not
ruled is what the customer is shown. Naming a substitute would promise that a specific person is coming,
which the pull board cannot deliver — and manufacturing exactly that kind of promise is what
`Q-PROMISE-02` was raised about. Default taken: **none**, because the two answers have different diffs
(a sentence vs. a name + a second picker) and one of them is a promise I may not make.

**`Q-ASSIGN-02` (blocking: no) — Invariant H is relaxed from 90% to 80%. Is two rounds the right price?**
Verbatim shape of the question: *a favourite-cleaner booking may now consume up to a fifth of its fill
window across two reservations before it is fully open to the board — where before it consumed a tenth
across one. Two rounds is derived from `MaxPreferredOfferRounds × PreferredHoldFraction ≤ 0.20`; one
round would keep ADR-0036's 90% and cost the customer their second choice; three would take it to 27%
and would break the invariant as a number rather than relax it. At what fill-rate cost do you want us to
stop?* Default taken: **2**, because it is the smallest number that satisfies the owner's *"offer
customer another"* clause at all. **We have no fill-rate measurement** — ADR-0036's own measurement
ticket is still a precondition — so this number is reasoned, not measured, and it is a `const` (a
backend release to change, no client impact).

**`Q-ASSIGN-03` (blocking: no) — does a cleaner who repeatedly declines lose the perk?**
Not built and not defaulted. A decline is currently free and invisible; whether repeated declines should
feed `GetMyServingCleaners`, the resolver, or a cleaner's rating is a supply-policy question that will
otherwise become folklore the first time someone abuses it. Default taken: **none** — nothing records
declines beyond the deadline write, so no policy can accrete by accident.

**`Q-ASSIGN-04` (blocking: no — but it decides whether a wave 2 exists) — is the *true* assignment ever
wanted?**
This ADR builds the **reservation** the owner's own *"ask employee to confirm"* clause describes: the
cleaner's silence leaves the job un-theirs. The stronger product — *the job is theirs whether or not
they answer, and the customer's cleaner arrives* — is a different system with different failure modes (a
cleaner who never read the push turns up nowhere and the platform has promised a person it cannot
deliver), and it would need the `OrderEmployee` row this ADR rejects, with all four of A1's costs paid
deliberately. Default taken: **the reservation**, on the owner's second clause.

---

## Open / undecided (not escalations — recorded so they are not re-derived)

- **The 5-minute cadence is reasoned from the 48-minute floor, not measured.** If ceiling-length (12 h)
  windows dominate in practice, 15 minutes would be cheaper and indistinguishable; the flip condition is
  the observed distribution of granted window lengths.
- **No `EXPLAIN` for the sweep's predicate.** It filters `PreferredHoldUntilUtc <= now` — an unindexed
  nullable column — with `PreferredOfferLapseNotifiedAt IS NULL`. The population is bounded by
  "reservations granted in the last window", which is small, and **D5.5's no-new-index posture is
  preserved** — but this is a reasoned expectation, not a measured plan, and the rig exists
  (`PostgresContainerFixture`). AC on R3.
- **The perk's reach is bounded by partner push adoption** (ADR-0036 D4.1). Under a silent hold that was
  a technicality; under a disclosed assignment it is a product fact: a customer's favourite who works
  from the partner web SPA can never be reserved, and the customer is never told why their favourite
  was not offered. No copy in this design says why — by D7.3 — so this is consistent, but it is a
  larger silence than it was.
- **Admin visibility of a live reservation** — still not decided (ADR-0036 left it open, and this ADR
  adds a customer-facing state that support will be asked about).

---

## Challenge

*(challengers write here — the specific hole, and why it matters, with the code/lifecycle/persona cited)*

## Defense

*(author answers each: REBUT with evidence · CONCEDE + REVISE · ESCALATE)*

## Verdict

*(lead adjudicates: each challenge RESOLVED or BLOCKING; consensus or escalation)*
