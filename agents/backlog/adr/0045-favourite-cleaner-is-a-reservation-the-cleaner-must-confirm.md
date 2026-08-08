# ADR-NNNN — The favourite cleaner is **assigned**, and assignment is a **reservation they must confirm**: ADR-0036's exclusivity mechanism is kept **byte-for-byte** and given the four things it lacks — a **name**, an explicit **decline**, a **customer-visible state**, and a **customer-facing exit** — where the reservation is **never an `OrderEmployee` row**, the release stays a **clock comparison with no actor**, the **confirm is `TakeOrder` unchanged**, and the **only new actor is a 5-minute sweep that ANNOUNCES the lapse and releases nothing**; the customer's re-offer is **capped at two rounds** (Invariant H relaxed 90% → **80%**) and *"a random cleaner"* is ruled to be **the open board, named** — not a second dispatch model

- **Status:** `accepted` — PM, 2026-08-08, on the round-3 lead's own terms: a delta review of the
  transcription against its closed list, no further adversarial round. Four things spot-checked at HEAD
  before accepting, not taken from the reports: the section the challenge found stale really is shipped
  (event, both producers, display registration, corrected copy, keyset on all three mirrors); the
  one-hour promise is now dropped on **both** platforms, so this ADR's standing constraint has a verify
  step that passes rather than one that fails; the two-round cap's 19%-against-20% margin and the three
  legs of the no-assignment-row argument were each re-derived by a second instance; and the transcription
  corrected 19 citations including one of the lead's own.
  **One ruling is mine rather than the lead's** and is marked as such in §PM ruling: the flag gains a
  fifth term so it cannot read `true` where the server refuses. The lead's four-term list predates the
  item that made recurring occurrences refuse; their interaction was never in front of it. The answer is
  forced by this ADR's own blocking finding — that a flag reading `true` where the mechanism cannot
  deliver is a defect — so shipping a second known instance of it in the same release is not a position
  this document can hold.
- **Superseded status line (transcription author):** `proposed` — drafted 2026-08-08 by the `architect` in **author** mode, on direct owner
  instruction recorded as the answer to `Q-PROMISE-02`; **revised the same day to fold in the owner's
  answers to all four questions the first draft escalated** (`Q-ASSIGN-01…04`, §Owner rulings).
  **Challenged 2026-08-08** (`adr/challenges/NNNN-favourite-cleaner-reservation.md`, nine findings, five
  blocking) and **adjudicated the same day by the `architect` in lead mode — see §Verdict. Five findings
  stand and the ADR was REVISED, not accepted**; the lead's closed edit list (V1–V10, 39 items) was
  **executed the same day by the author — see §Revision log**. Numbers are allocated at acceptance; this
  file lives in `drafts/` and its status is still `proposed`.
- **Date:** 2026-08-08 (drafted) / 2026-08-08 (owner answers folded in) / 2026-08-08 (challenge round
  executed)
- **Owner rulings this ADR carries, and none is reopened by a later reading:** `Q-ASSIGN-01` → **a
  promise, not a name** (§D5.2) · `Q-ASSIGN-02` → **two rounds stand** (§D5.3) · `Q-ASSIGN-03` → **not
  now, and it is a BUILD not a flip** (§D13) · `Q-ASSIGN-04` → **the reservation is confirmed**, plus a
  new owner sentence that adds a customer-facing confirmation message and a terminal state (§D10.2,
  §D10.3) · **`Q-PROMISE-03` → (c), drop the one-hour number** (2026-08-08,
  `questions/open.md:1981-1997`) — **this design is safe only because that number is going away; §D4.2
  carries the standing constraint that falls out of it.**
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
- **Composes with ADR-0035 in two places, one of which is a NON-interaction worth recording:** D2.1's
  placement rule puts this ADR's constants on `BookingPolicy` (§D5.3, §D11); and **the express waiver and
  the reservation are DISJOINT by construction** — express is lead ∈ [2, 4)
  (`BookingPolicy.RequiresExpressSurcharge`, `BookingPolicy.cs:130-140`, with `:20` / `:26`) and a hold
  needs lead ≥ 8 h (`:174`), so **no order can carry both**. §D8's express-waiver row is true and its set
  is empty. *(V9 — the earlier "the two perks collide over a cancellation" was false by construction, and
  a false composition claim sends a later reader to the wrong document.)* Also composes with
  ADR-0002/0008 (outbox, unchanged), ADR-0025 (push display, unchanged), ADR-0040 (`CurrentStatus` NOT
  NULL — relied on by the sweep's predicate).
- **Applies to:** `Cleansia.Core.Domain` (**two nullable/defaulted columns on `Order`**, one widened
  aggregate method, one derived state function — **no new entity, no new status, no change to
  `OrderVisibility`, no change to `OrderAvailability`**) · `Cleansia.Infra.Database`
  (⚠️ **`ef-migration`, owner-only**; additive, **no backfill**, **no new index**) ·
  `Cleansia.Core.AppServices` (one new partner command, one new partner query, one new customer
  command, one shared notifier, one sweep handler, **two** new `BookingPolicy` constants —
  `MaxPreferredOfferRounds` and `MinimumOpenBoardShare`, §D5.3) ·
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

  **and (verbatim, 2026-08-08, answering `Q-ASSIGN-04`) — the sentence that adds the two things the
  first draft did not have:**
  > *"They either have to confirm that they took this order and then there is a message for the
  > customer that it was confirmed. If it's declined then it's gonna propose to find another cleaner,
  > if no found and none confirmed then a random is assigned."*

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

> ## AC4 — the customer hears from us exactly once per reservation, and never about a person
>
> **A reservation produces exactly one customer-facing message: `order.cleaner_assigned` if it is
> confirmed, or `order.preferred_offer_closed` if it ends without a confirmation. Never both, never
> zero, and never one that names a decline, a delay, a reason, or a substitute's identity.**
>
> *(`order.cleaner_assigned` is not perk-scoped — it fires for every assignment on the platform — and it
> is **already shipped** (§D10.2), not built here. The one key this ADR mints is
> `order.preferred_offer_closed`. Within a reservation, exactly one of the two fires.)*

---

## Context — every citation below was re-anchored against HEAD by opening the file, 2026-08-08 (post-challenge)

> ⚠️ **Why this heading is worded that way, dated, and load-bearing.** The first draft's census carried
> the same warrant and was **wrong** — it had been read against a source file that a parallel lane had
> already changed, so a whole section designed shipped work and two verification steps would have
> deleted correct customer copy in five locales on two clients (CH-F1, §Verdict). The proximate cause was
> not carelessness: the living page this ADR reads first,
> `agents/architecture/decisions/preferred-cleaner-dispatch.md`, then said *"Nothing is shipped yet"*, and
> the drift is arithmetic — the draft cited four `NotificationEventCatalog` constants at `:30/:44/:52/:60`
> while HEAD has them at `:43/:57/:65/:73`, **every one off by exactly 13**, the size of the
> `OrderCleanerAssigned` block (`:26-37`) plus its blank line. The page now carries a dated correction
> banner. **The rule this leaves behind: a citation you did not open is a citation to delete.**

### The brief's premise about notifications is stale, and the correction shrinks this ADR

The task brief states *"the partner notification surface: today the **only** partner-targeted dispatch
is a 30-minute new-jobs digest."* **That was true when ADR-0036 was drafted and is no longer true.**
Verified at HEAD:

| Partner-targeted event | Where |
|---|---|
| `order.new_available` (the 30-min digest, a **count**) | `NotificationEventCatalog.cs:43` |
| **`order.preferred_offer`** (**one order**, produced inline in the create path) | `NotificationEventCatalog.cs:57`; produced at `OrderFactory.cs:192-205` (the key at `:196`, the args at `:197-201`) |
| `order.assignment_cancelled` | `NotificationEventCatalog.cs:65` |
| `payroll.invoice_paid` | `NotificationEventCatalog.cs:73` |

`NotificationFeedEventKeys.Partner` (`NotificationFeedEventKeys.cs:48-54`) lists all four. **The
per-order, time-sensitive, digest-bypassing partner push the owner asks for is already shipped**, with
its own documented contract: it does not stamp the digest watermark, and it rides
`NotificationCategory.NewJobsAvailable` (`NotificationEventCatalog.cs:91`) so a muted cleaner is not
pushed around their own mute (`NotificationEventCatalog.cs:45-57`).

**⇒ This ADR adds ZERO partner-targeted notification events**, and the claim is checkable rather than
asserted: `NotificationFeedEventKeys.Partner` is unchanged by this design, and `git diff` on
`NotificationEventCatalog.cs` adds exactly one constant, whose `GetCategoryFor` arm maps to
`NotificationCategory.OrderUpdates` — a **customer** category. §D10.

**The customer-side confirmation message the owner asked for on 2026-08-08 SHIPPED the same day, in an
independent lane, while this draft was being revised. This ADR neither designs it nor claims it.** §D10.2
records it as a **precondition already met**, with its citations, because a later reader who finds it
missing from an ADR that discusses the confirmation message will otherwise re-derive it.

### What else already ships (the whole of ADR-0036 and ADR-0039 landed while their living doc still said "nothing is shipped yet")

| Thing | Evidence (re-opened at HEAD, 2026-08-08) |
|---|---|
| The hold pair + its sole writer | `Order.cs:246`, `:264`, `GrantPreferredHold` `:424-435`, `ClearPreferredHold` `:438-443` (sole production caller `:689`) |
| The one visibility rule, both forms | `OrderVisibility.cs:36-52` |
| The resolver, all eight gates in order | `Cleansia.Core.AppServices/Services/PreferredCleanerHoldResolver.cs:23-101` |
| The window formula, floor and ceiling | `BookingPolicy.ComputePreferredHold` `:171-180`; `PreferredHoldFraction = 0.10m` `:159`; `PreferredHoldCeilingHours = 12` `:160`; floor `2 * StandardLeadTimeHours` where `StandardLeadTimeHours = 4` (`:20`), applied at `:174` |
| The targeted push | `OrderFactory.cs:175-205` (resolve `:175-182`, grant `:184`, push `:192-205`) |
| Enforcement at the six surfaces | `OrderSpecification.cs:169-173`; `OrderAccessService.cs:88-91`; `TakeOrder.cs:82-90`; `NewJobsDigestService.cs:138` |
| The digest's hold-expiry freshness disjunct | `NewJobsDigestService.cs:261-275` (both branches: `:262-265`, `:272-275`) |
| ADR-0039's set-based occupancy + the picker tri-state | `OrderRepository.cs:287-333`; `GetMyServingCleaners.cs:45-46`, `:92-145` |
| The Plus gate at creation | `CreateOrder.cs:162-171` |
| Recurring carry-through | `MaterializeRecurringBookingTemplate.cs:240` |
| **The customer's "a cleaner is on your job" message** (shipped 2026-08-08, independent lane) | `NotificationEventCatalog.cs:37` + arm `:89`; `OrderCleanerAssignedNotifier.cs:17-41`; producers `TakeOrder.cs:275-276` and `AdminReassignOrder.cs:102-103`; §D10.2 |

**So the owner's five asks decompose against shipped code as follows, and this is the single most
valuable paragraph in this document:**

| Owner's ask | State |
|---|---|
| *"he has to be **assigned**"* — the order is his to lose, not one row among many | **Mechanically SHIPPED.** ADR-0036 withholds the order from every other cleaner until the deadline. What is missing is that nobody — not the cleaner, not the customer — is ever *told* this. |
| *"send a notification to the employee when customer created an order"* | **SHIPPED.** `order.preferred_offer`, `OrderFactory.cs:196`. |
| *"ask employee to **confirm** the order"* | **SHIPPED as a mechanism, MISSING as a surface.** "Confirm" is `TakeOrder`. The held order appears only on the cleaner's ordinary available board, which is exactly why it reads as *priority*, not *assignment*. |
| *"if not…"* — an explicit refusal | **MISSING.** There is no decline. Silence is the only refusal, and it is indistinguishable from not having looked. |
| *"…then offer customer either select another employee… or suggest a random cleaner"* | **MISSING entirely.** The customer is told nothing at any point, and there is no way to change a preference after booking. |
| *"…there is a message for the customer that it was confirmed"* (2026-08-08) | **SHIPPED 2026-08-08**, in an independent lane, while this draft was being revised — key, category arm, one shared producer, both assignment call sites, the FCM arg map, the feed keyset and five-locale copy on both customer clients. **This ADR neither designs it nor claims it**; §D10.2 cites it as a precondition already met. |
| *"if no found and none confirmed then a random is assigned"* (2026-08-08) | **SHIPPED as a mechanism** — that is the open board, which is where a lapsed order already is. What is missing is the *sentence*, and §D10.3 rules what it may say. |

**Three of seven are missing and one is a surface; the other three already ship.** That is the real size
of this feature, and it is why this ADR spends most of its length protecting what exists rather than
building.

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
demands it — and **`Q-ASSIGN-04` has now confirmed it explicitly** (2026-08-08). The confirmation is not
a formality the platform may skip on the cleaner's behalf: *"They **either have to confirm** that they
took this order…"*. Silence leaves the job un-theirs, which is exactly what the reservation delivers and
exactly what a true assignment would not.

---

## D0 — What this supersedes in ADR-0036, exactly, and what it does not

**Three sentences and one number. Nothing else.**

| # | ADR-0036 text | Disposition |
|---|---|---|
| 1 | **AC3**: *"No assignment-model change is required, anywhere, for any order… this ADR changes who may see and take an order for a bounded interval, never whom an order belongs to."* | **SUPERSEDED in its first clause, KEPT in its second.** Whom an order belongs to still changes only at `TakeOrder`. What changes is that the interval is now named, refusable, disclosed and followed by a customer choice. |
| 2 | **D2 consequence #1**: *"Expiry needs no actor… There is **no sweep, no timer**, no outbox message, no status transition, no `IsActive` flip."* | **SUPERSEDED in its NOTIFICATION half only.** The **release** keeps every word: no sweep releases anything, no status moves, no column flips. A timer now **observes** expiry to notify the customer, and writes one receipt column it is the sole writer of. §D6 shows why the failure modes do not merge. |
| 3 | **D3 / Invariant H**: *"≥ **90%** of every seat's fill window is open to the entire board."* | **RELAXED to ≥ 80%**, enforced by a round cap of 2 rather than by the formula. §D5.3. This is the one genuine marketplace cost this ADR pays; it was escalated and is **ANSWERED** (`Q-ASSIGN-02`, owner, 2026-08-08 — two rounds stand). The bound is deliberately loose: the true worst case is **19%**, not 20%. |

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
> (`OrderRepository.cs:318-333`), whose status set is `{New, Pending, Confirmed, OnTheWay, InProgress}`
> (`:264-271`) — **and** the seven other resolver gates pass (`PreferredCleanerHoldResolver.cs:32-90`:
> preference set · signed in · active membership · cleaner exists · `IsActive` +
> `ContractStatus ∈ {Approved, Active}` (`:58-59`) · work country matches the service address · category
> not muted · at least one device with `NotificationsEnabled`).

**It is evaluated at exactly two moments, and this ADR adds no third.** The picker
(`GetMyServingCleaners.cs:135-136`) and the resolver (`PreferredCleanerHoldResolver.cs:108-112`) call
**the same method with the same window** — ADR-0039 AC2, which is what makes the customer-facing claim
honest. A third evaluation moment would produce a third answer.

**What if the slot fills between booking and confirmation?** *(the brief's question, and it is the one
place a reader will reach for a new mechanism)*

**Nothing re-checks it, deliberately, and nothing needs to.** Between the grant and the confirmation the
beneficiary can only become busy by taking another job — and the confirm **is** `TakeOrder`, whose
single ordered chain already ends in `NotHaveTimeConflictAsync` (`TakeOrder.cs:69-70`, `:211-227`).
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
   (`TakeOrder.cs:199-208`, tiers at `:201-206`). Three unanswered pending offers in a morning would
   exhaust a 3-cap cleaner's whole week.
2. **It blocks the cleaner's calendar against jobs they would have taken.** A row makes the order match
   `LiveCommitmentsInWindow(...).Where(o => o.AssignedEmployees.Any(...))`
   (`OrderRepository.cs:282-284`) — the very predicate `TakeOrder`'s conflict gate (`:273-274`) and
   ADR-0039's picker (`:302-307`) read. The cleaner is marked busy for a window they have not accepted.
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
| **c. Customer-visible state** | the state + the respond-by instant on the customer's order detail. *(The message when the cleaner confirms is **already shipped** — `order.cleaner_assigned`, §D10.2 — so it is a precondition here, not a cost.)* | ⚠️ `nswag-regen`, **no notification key** |
| **d. A customer-facing exit** | one message at lapse, and a **capped** re-offer (§D5) ending in the open board (§D10.3) | one sweep, two columns, **the ADR's one notification key** |

**The confirm is `TakeOrder`, unchanged.** Not a new command, not a widened one, not a bypass. The
button says "Confirm" instead of "Take"; the request is identical. This is what keeps
`TakeOrder.Validator`'s one ordered `Cascade.Stop` chain (`TakeOrder.cs:45-70`) the single write gate — a
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

#### D4.2 — The promise this design depends on being withdrawn (`Q-PROMISE-03`)

> **`Q-PROMISE-03` — ANSWERED by the owner 2026-08-08: option (c), drop the one-hour number**
> (`agents/backlog/questions/open.md:1981-1997`; the answer at `:1996-1997`). The post-booking screen
> stops stating an absolute time-to-assignment. **This ADR is safe only because that number is going
> away.**

**The arithmetic, from the shipped constants alone, so no reader has to take it on trust.**
`ComputePreferredHold` is `min(lead × 0.10, 12 h)`, zero below `2 × StandardLeadTimeHours`
(`BookingPolicy.cs:171-180`, with the fraction at `:159`, the ceiling at `:160` and the floor applied at
`:174`). So `0.10 × lead > 1 h ⟺ lead > 10 h`: **round one alone withholds the order from the whole
board for longer than an hour above a ten-hour lead**, before a second round exists. A 24-hour booking
withholds **2 h 24**. And §D7.2 puts `respondByUtc` on the customer's own order detail, so under the
previous copy the platform would have shown *"awaiting confirmation until 22:00"* on an order whose
booking-success screen said *"Within 1 hour"* minutes earlier, in the same app.

**Invariant H cannot rescue it, by type.** Invariant H is a **SHARE** of the fill window and is
structurally incapable of bounding an **ABSOLUTE** figure: its bound of 20% (`1 −
MinimumOpenBoardShare`) admits 33.6 hours on a 168-hour fill window, and the true withheld time at that
lead — 12 h at the ceiling twice, §D5.3 — is 24 hours. Neither number is an hour, and no share ever
becomes one. No constant in this design bounds an absolute figure, and **none may be added to fake it**:
an absolute cap composed onto
`ComputePreferredHold` would make the perk's value depend on a copy decision, and would have to move
every time the copy did.

> ### Standing constraint on every later reader
>
> **No surface may state an absolute time-to-assignment.** Not the booking-success screen, not the
> order detail, not a push, not an email, not a marketing string, in any of the five locales, on any of
> the six clients. A sentence of the form *"within N minutes/hours"* about **when a cleaner will be on
> the job** is forbidden, because nothing in a pull dispatch can keep it — an order can always reach its
> cleaning time unclaimed, which was true before this feature existed and stays true after it.
> **Reinstating such a number reopens this ADR and `Q-PROMISE-03` together**; it is not a copy call.
>
> Permitted, and it is the shape the owner's own answer names: a statement about **what the platform is
> doing** (*"we'll let you know as soon as we find one"*). Forbidden: a **deadline**, an **outcome**
> (*"a cleaner will be assigned"*), or a **person** (`Q-ASSIGN-01`, §D5.2, §D10.3).

**State of the withdrawal at HEAD, because it is a precondition rather than a ticket this ADR files.**
The copy edit runs in an independent lane and is **half landed**:

| Client | `booking_success_t2_desc` at HEAD | Carries an absolute duration? |
|---|---|---|
| Android customer, 5 locales | `values/strings.xml:759` *"We'll let you know as soon as we find one"*; `values-cs:748`, `values-sk:745`, `values-uk:745`, `values-ru:745` | **no — done** |
| iOS customer, 5 locales | `CleansiaCustomer/Resources/Localizable.xcstrings:4939-4972` — `en` *"Within 1 hour"*, `cs`/`sk` *"Do 1 hodiny"*, `ru` *"В течение 1 часа"*, `uk` *"Протягом 1 години"* | **NO — landed 2026-08-08**, values byte-identical to Android's after unescaping, and pinned by `BookingSuccessTimelineStringsTests` |

⇒ **The iOS half is a precondition of every customer-facing ticket below** (R6/R8/R9), not an item this
ADR files, and the verify step for the standing constraint fails today on exactly those five strings.
*(`Q-PROMISE-04` — the same screen's *"We'll remind you 1 hour before"* — was raised off this same edit
and is a separate question; it is **not** governed by the constraint above, which is scoped to
time-to-**assignment**, but it is the same shape and the same lane.)*

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
//   - PreferredHoldUntilUtc == null
//       || PreferredHoldUntilUtc <= nowUtc
//       || PreferredEmployeeId == preferredEmployeeId
//                                          (NO LIVE RESERVATION FOR SOMEONE ELSE — this is the
//                                           invariant that can actually fail)
//   - untilUtc > nowUtc                    (a grant must be in the future)
//   - PreferredOfferRound < BookingPolicy.MaxPreferredOfferRounds
```

> **Why the no-eviction invariant is phrased on the HOLD and not on the value it replaces (CH-F3).**
> The drafted guard was *"`untilUtc` > the current value"*, and **it was vacuous**:
> `untilUtc(t) = t + min(0.10·(L − t), 12)` is strictly increasing in `t` — slope 0.9 below the ceiling,
> 1.0 at it — so at `L = 24 h` a re-grant three minutes after booking yields **2.445 h** against a
> stored **2.4 h** and passes. It refused nothing in the grantable domain and **would have permitted
> exactly the silent eviction it named.**
>
> **And it keys on the HOLD, never on the preference column.** `Order.Create` (`Order.cs:387`) writes
> `PreferredEmployeeId` independently of any hold, so the customer's stored pick is non-null in the
> 2–8 h notify-only band and in every declined outcome. An invariant phrased on `PreferredEmployeeId`
> would refuse re-offers that never held anything and permit ones that do — backwards on both sides.

**The product consequence, stated so nobody reads the invariant as an oversight:** a live beneficiary
cannot be evicted, so **the customer's second choice becomes available when the reservation ENDS** —
which is the instant §D6's message reaches them, and is the owner's own ordering (*"if not… then offer
customer"*). §AC1's *"second and final choice"* is offered **at the lapse, never before it**.

`CreateOrder.Validator`'s eligibility rule (`CreateOrder.cs:168-170`,
`OrderRepository.UserHasCompletedOrderWithEmployeeAsync`) and its Plus gate (`:166-167`) are mirrored in
this command's validator as **the same two `MustAsync` calls in the same order** and with the same two
error keys — entitlement first, because it reveals least (`CreateOrder.cs:158-161`).

**Five structural refusals, and every one is silent about people:**

1. the **same** `EmployeeId` the row already carries (that is the person who just lapsed or declined —
   §D6 keeps the id on the row precisely so this is possible without telling the customer anything);
2. **any cleaner already assigned**;
3. **a live reservation belonging to someone else** — the invariant above;
4. **a `NotifyOnly` resolver outcome** — ruled explicitly here rather than left to the reader;
5. **a recurring occurrence** — `RecurringTemplateId != null`, ruled in §D6.2.

**On (4), the ruling is REFUSE, not grant-and-burn.** When the remaining lead has fallen below
`2 × StandardLeadTimeHours` the resolver returns `NotifyOnly(ShortLeadTime, …)`
(`PreferredCleanerHoldResolver.cs:94-98`, off `BookingPolicy.cs:174`). **A re-offer that cannot withhold
a seat is not a reservation**: it would push a named cleaner about an order the whole board already
holds, burn the customer's final round, leave `PreferredOfferState.None` (§D7.1), and then produce a
lapse message on the next sweep tick for a reservation that never existed.

All refusals collapse to **one** new key, `order.preferred_offer_closed`, per ADR-0036 D5.2's rule
(*never introduce an error key that names the exclusivity; reuse the most generic refusal the caller
could already have received*). Five locales × the three customer apps.

#### D5.2 — *"or suggest a random cleaner"* is **the open board, named** — and is not built

> **Ruling: "any cleaner" means the order is released to the board immediately. `PreferredHoldUntilUtc
> = now`, one write, no new mechanism, no new column, no engine.**

At lapse the order is *already* on the open board; the "random cleaner" choice is therefore the
**default**, and choosing it explicitly is an **acknowledgement, not a write**. The mechanism is
Cleansia's shipped pull dispatch: the first eligible cleaner takes it.

> **It writes nothing, and that follows from §D5.3 rather than being a separate ruling.**
> `GrantPreferredHold` is the **sole** writer of `PreferredOfferRound`, so "any cleaner" cannot stamp
> the counter to the cap, and it does not need to: the customer is asked **once per lapse**, guarded by
> `PreferredOfferLapseNotifiedAt` (§D6), and after round 2 the cap is exhausted anyway. *(An earlier
> draft said this choice "stamps the round counter to the cap"; that would have been a second writer of
> the counter, which §D5.3's sole-writer sentence forbids. Nothing else in the design depended on it.)*

**The alternative — the platform picks a specific substitute at random and offers them exclusively — is
rejected (A5).** It would withhold the order from every *other* eligible cleaner in favour of one chosen
by dice, which strictly *slows* the fill; and it would create a second dispatch model beside the pull
board, for a customer who has just told us they no longer care who comes.

**What the customer is shown — `Q-ASSIGN-01`, ANSWERED by the owner 2026-08-08: a PROMISE, not a
name.** The customer is told the platform will find them a cleaner; **no specific person is shown at
this step.** That is now a binding constraint on the copy and it is checkable:

> **The terminal sentence is about what Cleansia is DOING, never about an outcome or a person.**
> *"We're finding you a cleaner"* — permitted. *"A cleaner has been assigned"*, *"A cleaner will be
> assigned"*, or any substitute's **name**, **photo**, **rating** or **ETA** — forbidden.

Naming a substitute would promise that a specific person is coming, which a pull board cannot deliver;
and *"a cleaner **will** be assigned"* promises an outcome that **no** dispatch model can deliver
(§D10.3). Manufacturing exactly that kind of promise is the defect `Q-PROMISE-02` was raised about, and
reproducing it inside the fix would be the worst available outcome.

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

**`GrantPreferredHold` is the sole writer of `PreferredOfferRound` and increments it, so the creation
grant is round 1 and the re-offer is round 2.** Without that sentence `Round < Max` admits **three**
reservations (0→1→2, third refused only on the fourth call) and Invariant H's arithmetic below is wrong.
One writer, one increment, in the aggregate method that already owns the pair.

**Invariant H, restated:**

> **For every SEAT on every order, at least 80% of that seat's fill window is open to the entire board.**
> Two rounds × a tenth each, with the ceiling binding each round independently.

**The true maximum withheld share is 19%, not 20%.** With `h₁ = min(0.10L, 12)` and the re-offer taken
at the earliest instant it can be (`t = h₁` — a later re-offer withholds strictly less, and §D5.1's
no-eviction invariant is what makes `t = h₁` the earliest, since a re-offer before the lapse is
refused):

- `L ≤ 120`: `h₁ = 0.10L`, `h₂ = 0.10(0.9L) = 0.09L` → union `0.19L` → **19%**
- `L > 120`: `h₁ = 12`, share `(12 + h₂)/L` = `0.10 + 10.8/L` just above 120, and it **falls** thereafter
  (2.4% at `L = 1000`)

**Two rounds both at the 12-hour ceiling need `L ≥ 132`** (24 h / 132 h = **18.2%**); at `L = 120` the
worst case is **22 h 48**, not 24 h. *(The earlier "24 hours of a fill window at least 120 hours long"
paired the wrong two numbers and, at exactly 20%, read like a proof that the invariant is tight.)*

⇒ **`PreferredOfferInvariantTests`' `MaxPreferredOfferRounds × PreferredHoldFraction ≤ 1 −
MinimumOpenBoardShare` is a CONSERVATIVE OVER-ESTIMATE** — it sums fractions of the **original** lead
where round two only ever gets a fraction of the **remaining** lead. **It is not tight at equality; do
not read it as though it were**, and do not "fix" it into tightness. It errs in the safe direction, and
it is still the only thing pinning the two numbers together: **neither moves alone.**

**`Q-ASSIGN-02` — ANSWERED by the owner 2026-08-08: two rounds stand.** The derived cap is confirmed and
the Invariant H relaxation (90% → 80%) is accepted. **Settled; not reopened by any later reading.** The
number remains reasoned rather than measured — ADR-0036's measurement ticket is still a precondition —
and it is a `const`, so moving it is a backend release with no client impact.

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

**And the action the prompt would have triggered is refused on the same predicate: `ChoosePreferredCleaner`
is refused when `RecurringTemplateId != null`**, with `order.preferred_offer_closed`. Three reasons, all
cheap to check:

1. **It would not persist.** The choice changes one occurrence, while
   `MaterializeRecurringBookingTemplate.cs:240` re-grants the template's *original* favourite the
   following week. The customer's choice appears to stick and silently reverts.
2. **The cap would reset weekly.** `PreferredOfferRound` is per-**order**, so on a schedule's life
   *"exactly one re-offer"* would be false without bound — Invariant H still holds per seat, but the
   ADR's own framing would not.
3. **Suppressing the prompt while permitting the action is incoherent** — and the re-offer's own push to
   the newly chosen cleaner is not suppressed by §D6.2 either, so the interruption D6.2 exists to prevent
   would reappear on the supply side.

**The durable answer is F3 (a per-template preference surface); until it exists, a recurring customer's
exit is the template, not the occurrence.**

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
existence gate is `TakeOrder`'s (`TakeOrder.cs:82-90`) — the same
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

> **Amendment (CH-F4): `None` is correct for the STATE and wrong for the AFFORDANCE.** In the
> notify-only band `GrantPreferredHold` never runs (`OrderFactory.cs:184` only grants when the resolver
> returned a deadline), so `PreferredOfferRound` stays **0**, and a counter-only `canChooseAnother`
> reads **`true`** — offering a second choice for a perk that never applied and provably cannot. **The
> lead-time term in §D7.2 is what makes the state and the affordance agree.**

#### D7.2 — On the customer's order detail (⚠️ `nswag-regen`)

```
preferredOffer: {
  state:          PreferredOfferState      // derived above
  cleanerName:    string?                  // the person the customer themselves picked
  respondByUtc:   DateTime?                // the deadline; null unless AwaitingConfirmation
  canChooseAnother: bool                   // PreferredOfferRound < MaxPreferredOfferRounds
                                           //   && no assignment
                                           //   && BookingPolicy.ComputePreferredHold(
                                           //          order.CleaningDateTime, nowUtc) > TimeSpan.Zero
                                           //   && no live reservation
}
```

**`canChooseAnother` IS §D5.1's validator evaluated read-side** — the same terms, in the same order,
through the same function. **Never a re-implementation, and never a client-side lead-time constant**: a
client that hard-codes eight hours is a second copy of `BookingPolicy` on a device the platform cannot
redeploy. It is a **snapshot** and may go stale between render and tap (the deadline passes, someone
takes the job); the validator is the gate and the client tolerates the refusal. *(The list is the four
terms the panel ruled. §D5.1's recurring refusal has no counterpart term here — flagged in §Open rather
than added, because the term list was ruled explicitly.)*

**The dead tail this discloses, and it is a disclosed limit rather than a defect.** Round one lapses at
`creation + min(0.10L, 12)`; of the window left, **the last 8 hours can never carry a second
reservation** because `ComputePreferredHold` returns `Zero` below `2 × StandardLeadTimeHours`
(`BookingPolicy.cs:174`):

| Lead at creation `L` | Round-1 hold | Window left after lapse | Dead tail | Share of that window where the exit is unreachable |
|---|---|---|---|---|
| 8 h | 48 min | 7 h 12 | 7 h 12 | **100%** |
| 12 h | 1 h 12 | 10 h 48 | 8 h | **74%** |
| 24 h | 2 h 24 | 21 h 36 | 8 h | **37%** |
| 120 h | 12 h (ceiling) | 108 h | 8 h | **7%** |

**The customer's exit is a LONG-LEAD affordance by construction.** On short leads the honest answer is
that the order is already on the open board — which is exactly what the terminal copy says (§D10.3), so
the flag reading `false` there costs the customer nothing they could have had. **A flag that hides the
bound is worse than a feature that has one**, and this one is worst precisely where it matters: the
lapse notification exists because the customer *was not watching*, so the customer most likely to tap
"choose another" is the one who reads the push hours later — the population the dead tail is made of.

**A nested optional block, so a client that has not been rebuilt is unaffected.** ⚠️ **Both mobile
customer clients carry the row-dropping mapper idiom** ADR-0039 flagged, verified at HEAD:
`OrderApi.kt:369-372` (`GenGetMyServingCleanersResponse.toAppDto()` — three `?: return null` in a row)
and `ServingCleanersClient.swift:19-24` (`compactMap` + `guard let` into a non-optional struct). An
absent `preferredOffer` must **not** drop the order row. That is an AC on the mobile ticket, pinned by
an automated test per client, not by review.

**`respondByUtc` is an instant, not a countdown.** A countdown is a client concern and an anxiety
machine; an instant is a fact, and it is the fact the customer needs because §D6's prompt arrives at
roughly that time. Rendering it as relative time is the client's choice. *(It is also one half of the
timing side channel §D7.3 concedes — kept deliberately, with the trade priced there.)*

#### D7.3 — The customer is **never** told which way the offer ended

> **One sentence covers both a decline and a silence. The customer learns that the offer ended, and is
> offered a second choice. They are never told that a specific person refused, and never told that a
> specific person did not answer.**

> ### Conceded (CH-F5): the ARRIVAL TIME is a side channel and this ADR does not close it
>
> The customer holds `respondByUtc` (§D7.2) and a decline announces **immediately** (§D6.4), so a
> message far before the deadline means the reservation ended early and one at the deadline means
> nobody answered. **The property this design actually has, restated:** *the platform never states, on
> any surface, which way an offer ended, and never attributes conduct to a named person.*
> **Non-inferability is not claimed.**
>
> Three reasons this is accepted rather than closed:
>
> 1. **The bit is confounded.** An early close also fires from §D1.1 — the beneficiary took a
>    conflicting job — so what is recoverable is *"the reservation ended early"*, not *"they refused"*.
> 2. **Closing it costs the customer the scarce resource.** Announcing both outcomes at the deadline is
>    the only fix that works (dropping `respondByUtc` does not — the message's own arrival is the
>    signal). On a 12-hour booking, a decline at T+5 min announced immediately leaves **3 h 55** of live
>    re-offer window; announced at the deadline it leaves **2 h 48** — a **29% cut**, out of exactly the
>    window §D7.2's dead-tail table shows is the binding constraint.
> 3. **`Q-AVAIL-04`'s posture is MINIMIZATION, not non-disclosure.** This design makes **no statement
>    about a worker at all** — that is the property the legal argument rests on, and it survives the
>    concession intact. **If `Q-AVAIL-04` resolves against any disclosure, the closing move is "announce
>    at the deadline" — a change to the notifier's CALLER, not its content**, and copy-free. That the
>    fix is cheap and local is why deferring it is defensible.

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

**The writer census: which shipped writers this ADR affects.** `TakeOrder.Handler` — **exactly ONE
change**: the implicit-decline write (D1.1). *(The notification swap the first draft also billed here is
shipped — `TakeOrder.cs:275-276`, outside the `statusChanged` guard at `:267-273`, which survives on the
status-track append and on the email at `:278`. §D10.2.)* `AdminReassignOrder.Handler` — **no change**;
it already produces `order.cleaner_assigned` at `:102-103`. **Nothing else.** `AdminReassignOrder`'s
*assignment* behaviour (`:88`, `:100` — the only production caller of `UnassignEmployee` and the only
non-take assigner, verified as the only two production `OrderEmployee.Create` call sites alongside
`TakeOrder.cs:264`) is untouched: an admin assignment during a live reservation already consumes it
through `OrderVisibility` term 5 (`AssignedEmployees.Any()`, `OrderVisibility.cs:41`/`:52`), which
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

⚠️ **One defect this surface must not inherit.** `OrderAccessService.CanBrowseOrderAsync`
(`OrderAccessService.cs:88-91`) conjoins `HasAvailableSpots` and `NotHeldFrom` but **not**
`IsOfferableSql`. So a beneficiary can open the detail of a `New` + **Card** order whose money has not
landed — an order `TakeOrder.cs:55-56` will refuse with `order.not_takeable`, and which
`CleanupStalePendingOrders` may cancel within ~1 h 15 m.
Under a *disclosed* reservation that becomes "you were assigned a job that vanished". **The pending-
offers list carries the conjunct from day one**; whether `CanBrowseOrderAsync` should also carry it is a
pre-existing question and is **filed, not decided here**.

### D10 — Notifications: **zero new partner events, ONE new customer event, and zero partner-side copy**

#### D10.1 — The partner side, with its evidence

| Direction | Event | New? |
|---|---|---|
| → cleaner, at booking | `order.preferred_offer` | **no** — shipped (`NotificationEventCatalog.cs:57`, produced `OrderFactory.cs:192-205`) |
| → cleaner, at lapse | *(none)* | **deliberately none.** "You missed a job" is a negative-reinforcement push on a channel the cleaner can mute wholesale. |
| → cleaner, reminder before the deadline | *(none)* | **out of scope, with a flip condition** (A9): a measured non-response rate on ceiling-length windows. Not invented without evidence. |

**`NotificationFeedEventKeys.Partner` (`NotificationFeedEventKeys.cs:48-54`) is unchanged by this ADR.**
That is the check, not the claim.

##### D10.1.1 — The push keeps ADR-0036 D4.1's WIDER predicate **and** its current copy; the assignment framing lives on the surface, not on the lock screen

The announcement predicate is deliberately wider than the reservation predicate: `OrderFactory.cs:184`
grants on the resolver's **deadline**, `:192` pushes on the resolver's **recipient**, and a `NotifyOnly`
outcome carries a recipient with no deadline (`PreferredCleanerHoldResolver.cs:94-98`). **The key
therefore fires in two states its args cannot distinguish** — `orderId` + `orderNumber` only
(`OrderFactory.cs:197-201`), one per-event template map (`FcmMessageFactory.cs:32-33`).

⇒ **Copy naming a reservation would be false in the 2–8 h notify-only band, and the deadline cannot be
rendered from those args at all** (a `respondByUtc` arg is a backend change **plus** both partner
clients). The shipped five-locale copy — *"A customer asked for you / Order %1$s — someone you've
cleaned for before requested you. Open it to take the job."*
(`partner-app/src/main/res/values/strings.xml:1244-1245` and its iOS mirror) — **is true in both states
and stays.** **⇒ this ADR budgets no partner-side copy wave, and that is a decision, not an omission.**

**The push's deep link targets the order detail, as today** — not the new pending-offers surface —
**precisely because in the notify-only band there is no pending offer to show.** §D9's list is
`PreferredHoldUntilUtc > nowUtc`; a notify-only recipient following a deep link to it would find it
empty.

**Push-before-offerable is accepted and bounded, not closed.** A card order is `New` + `Card` +
`Pending` until the webhook, so §D9's `IsOfferableSql` conjunct correctly keeps it **off** the
pending-offers surface for that interval while the push has already landed. The residual — the cleaner
following the deep link into a detail page `TakeOrder.cs:55-56` will refuse — is the **F1 browse gap**,
filed. Narrowing the push instead is **A16, rejected**.

#### D10.2 — *"there is a message for the customer that it was confirmed"* — **the confirmation message is a PRECONDITION ALREADY MET**

**This ADR designs nothing here and claims nothing here.** The owner's 2026-08-08 sentence asked for a
customer-facing confirmation message; it **shipped the same day in an independent lane**, while this
draft was being revised. Every element is in the tree at HEAD, re-opened by the author on 2026-08-08:

| Element | At HEAD |
|---|---|
| The key | `NotificationEventCatalog.cs:37` — `OrderCleanerAssigned = "order.cleaner_assigned"`, doc-comment `:26-36` |
| Its category arm | `NotificationEventCatalog.cs:89` → `NotificationCategory.OrderUpdates` |
| One shared producer, so the two call sites cannot disagree | `Cleansia.Core.AppServices/Features/Orders/OrderCleanerAssignedNotifier.cs:17-41` (guest orders skipped at `:24-27`) |
| Producer 1 — the take **and** the preferred cleaner's Confirm (one command) | `TakeOrder.cs:275-276`, called **outside** the `statusChanged` guard at `:267-273`; the guard survives on the status-track append (`:269-273`) and on the status **email** (`:278`) |
| Producer 2 — the other path that creates an assignment row | `AdminReassignOrder.cs:102-103` |
| The FCM arg map | `FcmMessageFactory.cs:33` |
| The feed keyset | `NotificationFeedEventKeys.cs:35`, pinned by `NotificationFeedEventKeysTests.cs:63` |
| Both customer clients render it | `CustomerFeedEventKeys.kt:18`; iOS `CustomerFeedEventKeys.all` in `CleansiaCustomer/Sources/Features/Notifications/NotificationFeedTemplates.swift:9`; templates `NotificationTemplates.kt:28-30`; strings `customer-app/…/values/strings.xml:1213-1214` × 5 locales; iOS `Localizable.xcstrings:24084` (body) / `:24119` (title) × 5 locales |
| `order.confirmed`'s copy already corrected | `customer-app/…/values/strings.xml:1211` = **"Booking confirmed ✅"** |
| Its two honest producers, and only those | `HandlePaymentNotification.cs:279`, `ConfirmRecurringOrder.cs:126` — pinned by `OrderConfirmedHonestProducerTests.cs:66,92` |
| The card-path test | `OrderCleanerAssignedNotificationTests.cs:70,89,93,128,152` — incl. `Taking_An_Order_Never_Claims_A_Cleaner_Through_OrderConfirmed` (`:93`) and the category assertion (`:152`) |

**The one ruling §D10.2 keeps of its own:**

> **`order.preferred_offer_closed` is minted in the same shape as the shipped `order.cleaner_assigned`
> — one key, category `OrderUpdates`, one shared producer with two callers — and inherits the same
> sequencing rule** (`NotificationFeedEventKeys.cs:26-28`: *"A key belongs in a keyset only once the
> audience's clients render it: the unread badge counts every row in the keyset, so a key listed ahead
> of its client template inflates the badge with a row the app drops unrendered."*). **The customer
> keyset gains it only after both customer clients render it.** The push itself is unaffected — it is
> dispatched off `NotificationEventCatalog`, not the feed keyset; only the feed listing waits.

*(Recorded because the shape is the reusable part, not because this ADR built it: the shipped key exists
**because** `Confirmed` is overloaded — *"money settled" OR "cleaner assigned"* — so a key produced from
all of that column's writers says something false for some of them. That generalisation is the catalog
edit this ADR still carries; see §Catalog edits.)*

#### D10.3 — *"if no found and none confirmed then a random is assigned"* — the terminal state

**Mechanism: the open board.** After the second round lapses the order is already there — the clock put
it there — so "a random cleaner" needs no code at all. **The platform does not pick anyone.** A5's
rejection stands and is strengthened by `Q-ASSIGN-04`'s own first clause: a cleaner must *confirm*, so a
model that puts a job on someone who never answered contradicts the same sentence.

> ⚠️ **The sentence outruns the mechanism, and no available mechanism closes the gap — so the copy must
> close it.** *"a random **is assigned**"* implies a completion: at the end of the ladder, somebody is on
> the job. **An open board cannot guarantee that** — an order can reach its cleaning time unclaimed,
> which is the ordinary shipped outcome ADR-0036's Invariant H exists to bound (*"the hold can never be
> the reason an order goes unfilled"* concedes that orders do). **And actively assigning someone would
> not close it either** — an assignment the cleaner never read produces a customer who was promised a
> person and a flat nobody visits, which is a *stronger* false promise, not a weaker one.
>
> **Nothing in dispatch can promise a cleaner. Only an operational commitment can** (admin dispatch, a
> staffed fallback) — a business capability, not an architecture.
>
> **⇒ The terminal copy inherits `Q-ASSIGN-01`'s answer verbatim: a promise to FIND, never a claim that
> one IS found.** The owner's own words for that step — *"the platform will find them a cleaner"* — are
> the specification, and they are already the right sentence.

**Customer-side events, final:**

| Event | When | New? |
|---|---|---|
| **`order.preferred_offer_closed`** — args `orderId`, `orderNumber` | a reservation ended with no confirmation (sweep, §D6) or was declined (§D6.4) | **YES — and it is the ONLY key this ADR mints.** Category `OrderUpdates` — an update about the customer's own order must not need its own opt-out to be discoverable. |
| `order.cleaner_assigned` — args `orderId`, `orderNumber` | any cleaner is assigned to any order | **no — shipped 2026-08-08** (§D10.2) |
| `order.confirmed` | booking/money confirmed | **no** — and its copy is already corrected at HEAD (`values/strings.xml:1211`) |

**Does the 30-minute digest still make sense beside this?** Yes, and the two do not compete. The digest
emits a **count** of open board work and stamps the per-cleaner digest watermark; the targeted offer is
**one named order** and deliberately does neither (`NotificationEventCatalog.cs:39-43` vs `:45-57`,
whose doc-comment states the rule). That separation
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
| A re-grant could silently evict a live beneficiary who was told the job was theirs | **handled** — D5.1's no-eviction invariant (the drafted one was vacuous) |
| The customer's exit reads available where the server must refuse (short lead, notify-only band, live reservation) | **handled** — D7.2's four-term flag + D5.1's `NotifyOnly` refusal |
| `order.confirmed` claimed a cleaner where none existed | **NOT this ADR's** — shipped fixed 2026-08-08 in an independent lane; D10.2 cites it as a precondition met |
| The targeted push fires wider than the reservation (notify-only band, pre-webhook card orders) | **ruled, no wave** — D10.1.1; A16 rejected; the browse half is F1 |
| `CanBrowseOrderAsync` omits the offerability conjunct, so a beneficiary can open a `New`+Card order that may be cancelled under them | **filed, not fixed** — D9 / F1 |
| The web customer wizard has **no preferred-cleaner picker at all** (`order-wizard.facade.ts:575-577` leaves `preferredEmployeeId` off the JSON), so a web customer cannot use the perk or the re-offer | **filed** — pre-existing, called out because the customer-side exit is meaningless without it |
| The perk is effectively **mobile-cleaner-only** until the partner web SPA registers push devices (ADR-0036 D4.1's reachability gate) — so a customer's favourite who works from the web board can never be reserved | **pre-existing, restated** — under "assigned" it is a bigger product fact than it was under a silent hold |

### D13 — Declines are **not recorded**, and the successor to that is a **build**, not a config flip

**`Q-ASSIGN-03` — ANSWERED by the owner 2026-08-08: not now.** No decline policy, nothing enforced, and
no cleaner is penalised for refusing.

**The reason this needs its own decision section instead of a line in a table** is that the *next*
reader will assume it is a switch, and it is not:

> **A `DeclinePreferredOffer` writes `PreferredHoldUntilUtc = now` and stores nothing else. After the
> write, the platform cannot tell a decline from a silence, and it cannot tell how many either has
> produced.** There is no decline row, no counter, no timestamp, no reason. That is deliberate — a
> policy nobody has ruled must not accrete from data collected "just in case" — but it means **any
> future rule of the form "a cleaner who declines N times loses X" requires a schema change and a
> backfill-less start date first, and it can never be applied retroactively.**

**⇒ Recording declines is the precondition, and it is `A8`'s `PreferredOffer` child table** (the audit
row per offer), which is the same artifact "never re-offer the same person across rounds" would need.
When either is wanted, they are one ticket, not two — and until then this ADR's answer to *"can we just
turn it on?"* is **no**.

*(One consequence worth stating so it is not read as an oversight: `Order.PreferredOfferRound` counts
**rounds**, not declines, and it is per-order. It can never answer a question about a cleaner.)*

---

## Alternatives considered and rejected

| # | Alternative | Why not |
|---|---|---|
| **A1** | **A real `OrderEmployee` row at booking, with a confirmation state on the row** | The obvious reading of *"assigned"*. Rejected on three pieces of shipped code, **each re-derived at HEAD by the challenger and again by the lead**: `GetEmployeeOrderCountThisWeekAsync` (`OrderRepository.cs:245-257`) has no status term and no confirmation term, so the row spends the cleaner's weekly cap; `LiveCommitmentsInWindow` (`:318-333`) + `:282-284` makes the row block their calendar, on the one predicate both `TakeOrder`'s conflict gate and ADR-0039's picker (`:302-307`) read; and `CancellationAssessor.cs:55` (`var hasBeenAccepted = order.AssignedEmployees.Count > 0;`) makes the customer's cancellation fee-bearing (`BookingPolicy.cs:252-255`) and burns their express waiver (`CancelOrder.cs:143-146`) from the instant of creation. Undoing any of it requires a confirmation term inside the occupancy predicate — a **second definition of "occupied"**, which ADR-0039 D3.2 rejects and which fails in the double-booking direction. |
| **A2** | **A new `OrderStatus` (`AwaitingCleanerConfirmation`)** | The fulfilment axis is a property of the *work*. A multi-seat order can have one confirmed and one pending cleaner and one order-level status cannot express that. It also puts a new integer on the wire to three generated clients, forces an arm into `OrderAvailability` and therefore into ten surfaces, and is exactly the "second source of truth for one fact" ADR-0037 D5 forbids. |
| **A3** | **A sweep that releases the reservation** (deletes/flips it, then notifies) | Reintroduces the failure mode ADR-0036 D2 was written to eliminate: *an order stuck reserved*, with no actor permitted to clear it if the timer is down. Splitting release (clock) from announcement (sweep) costs one column and keeps the catastrophic failure unreachable. |
| **A4** | **Read-time expiry with the state on the assignment row** — every seat count learns a clock | Threads `now` through `Order.AvailableSpots`/`HasAvailableSpots` (`Order.cs:136-137`), `OrderSpecification.cs:141` **and** `:163`, `NewJobsDigestService.cs:135`, and `OrderMappers.cs:94-95`. Five seat-count surfaces, each of which can be forgotten independently — the sprawl ADR-0036 D5 and ADR-0037 D0 exist to prevent. |
| **A5** | **The platform picks a random substitute and offers them exclusively** — reading (ii) of *"a random is assigned"* | Withholds the order from every other eligible cleaner in favour of one chosen by dice, which strictly slows the fill, and manufactures a second dispatch model beside the pull board. **And it contradicts `Q-ASSIGN-04`'s own first clause** — *"they either have to confirm"* — since a job placed on someone who never answered is precisely what that clause forbids. **Crucially it does NOT buy the guarantee the sentence implies:** an assignment the cleaner never read produces a customer promised a person and a flat nobody visits, which is a *stronger* false promise than an unfilled board slot. *"Random cleaner"* is the board, named (D5.2 / D10.3). |
| **A14** | **Reuse `order.confirmed` for the confirmation message** instead of a dedicated key | **Not this ADR's decision — recorded because the shipped answer is the same one and a later reader will re-ask.** `order.confirmed` has two honest producers with no cleaner on the job (`HandlePaymentNotification.cs:279`, `ConfirmRecurringOrder.cs:126`), so reusing it would either fire twice for card customers or keep a claim that is false for those producers. **Widening it to mean both things is the overloading `CLAUDE.md` warns about, one layer up.** The tree already carries the dedicated key (`NotificationEventCatalog.cs:37`) and the pins that keep the two apart (`OrderConfirmedHonestProducerTests.cs:66,92`). §D10.2. |
| **A6** | **Automatic cascade to the next serving cleaner, without asking the customer** | Rejected on the owner's own words — *"offer customer either select another employee… or suggest a random cleaner"* — and because each automatic round spends fill window with no human deciding it is worth spending. |
| **A7** | **Tell the customer the cleaner declined (or did not answer)** | ADR-0039 D7's neutral-line rule, adopted: a sentence about what Cleansia can offer stays true when the predicate widens; a sentence about what a person did becomes a lie. Plus `Q-AVAIL-04` (lawful basis for disclosing a worker's state to a third party) is **open**, and the one benefit it buys is bought mechanically by D5.1's same-id refusal. |
| **A8** | **A `PreferredOffer` child table** (an audit row per offer) | Richer — it would support "never re-offer anyone who already declined" across rounds and an offer audit — but it is a table, a migration and a new aggregate member to carry a counter that one `int` carries. **Recorded as the durable answer with its flip condition:** a product need for cross-round exclusion, or an audit requirement on offers. |
| **A9** | **A reminder push before the deadline** | Doubles volume on a category cleaners can mute wholesale, on no evidence. Flip condition: a measured non-response rate on ceiling-length (12 h) windows. |
| **A10** | **Keep the hold and fix only the copy** (`Q-PROMISE-02` option (a)) | The owner chose option (b) explicitly. Off the table. |
| **A11** | **Put the window on `CountryConfiguration` now** | It carries no scheduling number today (`:11-96`); the first one is its own decision, and ADR-0036 already priced the later flip at "a change to the computation, no schema change". |
| **A12** | **One column instead of two** — reuse `PreferredOfferLapseNotifiedAt` as the round counter | Collapses two facts with two writers (the customer's action; the sweep's receipt) into one, which makes *"declined, customer not yet told"* inexpressible — the exact collapse ADR-0036 D2's two-column ruling exists to prevent. |
| **A13** | **A dedicated `ConfirmPreferredOffer` command** instead of reusing `TakeOrder` | A second write path into assignment, which would either duplicate `TakeOrder.Validator`'s ordered chain (`TakeOrder.cs:45-70` — approval · profile · cap · conflict) or be weaker than it. ADR-0037 D6 already established that a second chain in that validator breaks the first. The confirm is a **label**, not a command. |
| **A15** | **A customer-side "release my favourite now" action** — let the customer end a live reservation early instead of waiting for the deadline (the obvious answer to D5.1's no-eviction invariant) | **Rejected: it is a decline performed by the wrong actor.** The beneficiary was told the job was theirs to confirm (`order.preferred_offer`, and the pending-offers surface §D9), and ADR-0036 D4 forbids ever telling them they were passed over — so the release would **silently delete a push they already received and a row they are currently looking at**, with no sentence the platform is allowed to say about it. The reservation is short by construction (48 min at the floor, ≤12 h at the ceiling) and the customer's own cancellation is free throughout (§D8), so the exit they actually need already exists. **Flip condition:** a measured rate of customers cancelling outright during a live reservation. |
| **A16** | **Narrow the targeted push to `Granted ∧ IsOfferable`** — only notify the cleaner when a reservation truly exists and the order is takeable | **Rejected on two grounds.** (1) It makes the perk depend on **payment-rail latency**: the hold clock starts at creation either way (`OrderFactory.cs:184`), so a push deferred until the Stripe webhook lands spends the reservation waiting, and the cleaner gets a shorter window for a reason that has nothing to do with them. (2) It would **supersede ADR-0036 D4.1** — panel-settled, *"notify on a wider predicate than the hold"*, which is what makes the 8-hour floor cheap — to buy a lock-screen distinction the cleaner cannot act on differently. The honest fix for the notify-only band is copy that is true in both states, which is what ships (§D10.1.1). |

---

## How a reviewer verifies compliance

*(Re-numbered after the challenge round. Three steps were deleted as already-true-at-HEAD and one was
**inverted** — the deleted `grep -rn "Cleaner found"` step would have destroyed correct copy. §Revision
log maps old numbers to new.)*

1. **`git grep -n "OrderEmployee.Create"`** returns exactly the two production call sites it returns
   today (`TakeOrder.cs:264`, `AdminReassignOrder.cs:100`; everything else the grep returns is a test).
   **A third is a rejection of D2.**
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
   `cap <= floor`. **Read it as a conservative over-estimate (19% actual against a 20% bound, §D5.3);
   a reviewer must not "tighten" it.**
9. **The digest still sees lapsed orders.** A test that creates an order with an expired reservation and
   an un-run/already-run sweep, and asserts `NewJobsDigestService` still counts it for a third cleaner.
   This is the D6.1 regression guard and it is the one most likely to be lost.
10. **`grep -n "preferredOffer" ` in both mobile customer mappers**: the field's absence must produce a
    row with `state == none`, never a dropped order. One automated test per client. The idiom to look
    for is the shipped one at `OrderApi.kt:369-372` and `ServingCleanersClient.swift:19-24`.
11. **No country code appears in any new handler or validator.** `grep -niE '"(cz|sk|ua|de|pl|us)"'`
    over the new files returns nothing.
12. **`NotificationFeedEventKeys.Partner` is byte-unchanged.** That is the check behind "zero new
    partner events"; the claim is not asserted anywhere without it.
13. ⚠️ **THIS ADR REMOVES NO CUSTOMER COPY.** `notification_cleaner_assigned_title` / `_body`
    (`customer-app/…/values/strings.xml:1213-1214` + the four sibling locales) and
    `push.order.cleaner_assigned.title` / `.body` (`CleansiaCustomer/Resources/Localizable.xcstrings:24119`
    / `:24084`, five locales) **must still exist after this work**, and must be reachable only from
    `order.cleaner_assigned`. *(An earlier draft carried the inverse of this step —
    `grep -rn "Cleaner found" src/` must return nothing — which at HEAD would delete the one honest
    sentence in the set, in five locales on two clients. It is recorded here inverted rather than
    silently dropped.)*
14. **No terminal-state string asserts an outcome.** The five locales for the "any cleaner" step
    contain no word meaning *assigned*, no cleaner name, photo, rating or ETA — `Q-ASSIGN-01` / D10.3.
    A copy review item, listed here because it is the whole of the ruling's enforcement.
15. **No copy introduced by this ADR states a time-to-assignment**, and the booking-success pair
    (`booking_success_t2_title` / `_t2_desc`) carries **no absolute-duration string in any of the five
    locales on either mobile client** — §D4.2's standing constraint. At the time of writing this step
    **fails on iOS** (`CleansiaCustomer/Resources/Localizable.xcstrings:4939-4972` still says *"Within 1
    hour"* and its four translations) and **passes on Android** (`values/strings.xml:759`,
    `values-cs:748`, `values-sk:745`, `values-uk:745`, `values-ru:745`). Closing the iOS half is a
    precondition of R6/R8/R9, not a ticket this ADR files. A step that passes today would not have
    caught what `Q-PROMISE-03` caught.
16. **A re-grant cannot evict a live beneficiary.** Grant to A; call `ChoosePreferredCleaner(B)` while
    A's hold is live → **refused** with `order.preferred_offer_closed`; call it again after
    `PreferredHoldUntilUtc` → **accepted**. (D5.1. The invariant this replaces passed for every `t > 0`.)
17. **The exit is not offered where the server refuses it.** A same-day order (2–8 h lead) returns
    `canChooseAnother == false` on the customer DTO, **and** `ChoosePreferredCleaner` on it is refused.
    Separately, `ChoosePreferredCleaner` on a recurring occurrence (`RecurringTemplateId != null`) is
    refused (§D6.2) — see §Open on whether `canChooseAnother` should carry that term too.
18. **`order.preferred_offer_closed`'s args and rendered copy are byte-identical on both paths** — the
    5-minute lapse sweep and the immediate decline. **That equality is the whole of §D7.3's guarantee**;
    a per-path string would re-introduce the disclosure the neutral line exists to prevent.

---

## Consequences

**Positive**
- The owner's seven asks are delivered with **two new columns, one timer, three commands, one query,
  ONE notification key and zero changes to any of the three shipped rules** (`OrderAvailability`,
  `OrderVisibility`, `LiveCommitmentsInWindow`).
- The customer's promise becomes true in all five locales at once: `cs`/`sk`/`ru`'s *"will be
  preferentially assigned"* is now backed, and `en`/`uk`'s *"prioritized"* is the understatement the
  owner named. **T-0544 / T-0491's copy work gains its ruling** and the affirmative wave is now owed.
- A cleaner can say no in one tap, which is the first supply-side control this feature has ever had.
- **The customer's exit discloses its own bound.** `canChooseAnother` carries the lead-time term, so the
  affordance and the server agree; §D7.2 states the dead tail (100% / 74% / 37% / 7%) rather than
  shipping a button that fails.

**Negative, and priced**
- **Invariant H drops from 90% to 80%.** Two reservations on one booking. **Owner-ruled** (`Q-ASSIGN-02`,
  2026-08-08) on a reasoned rather than measured number. The true worst case is **19%**, so the pinned
  bound is conservative (§D5.3) — but it is still a real marketplace cost, not a rounding artefact.
- **There is now an actor in this feature.** ADR-0036 had none. The failure modes do not merge (D6/AC2),
  but the operational surface grew by one timer and one receipt column.
- **This design depends on a promise being withdrawn.** `Q-PROMISE-03`'s (c) — the booking-success
  screen drops the one-hour number — is a **precondition**, and it is **met on both platforms as of 2026-08-08**
  (§D4.2). Every customer-facing ticket below inherits it, and §D4.2's standing constraint outlives it.
- **⚠️ Two owner-only manual steps**: an `ef-migration` (two additive columns, no backfill, no index) and
  an `nswag-regen` (one nested optional DTO block + two customer endpoints + two partner endpoints).
- **Six clients change**: three customer apps (state block, re-offer, one error key × 5 locales, **one
  new push/feed template**) and three partner apps (a pending-offers surface + a decline). The web
  customer app cannot use any of it until it gets a picker at all — filed.
- **One new customer notification event** × 5 locales × 3 customer apps. **Zero partner-side copy work**
  — a decision, not an omission (§D10.1.1), and zero corrective copy: the `order.confirmed` correction
  and the `order.cleaner_assigned` wave both shipped on 2026-08-08 in an independent lane.
- **The neutrality is minimisation, not non-inferability.** The arrival time of
  `order.preferred_offer_closed` leaks *"the reservation ended early"* against a deadline the ADR
  deliberately discloses. Conceded in §D7.3, priced there, and closable later at the notifier's caller.
- **The terminal state is a promise the platform cannot guarantee, and this design makes that more
  visible, not less.** Before, an unfilled order was silent; now the customer has been walked down a
  ladder that ends there. The copy is constrained to a promise-to-find (D10.3), but **the residual — the
  rate at which orders reach their cleaning time unclaimed — is pre-existing, unmeasured and unbounded**,
  and nothing in dispatch can close it. See §Open.

**Tickets this ADR asks the PM to file** *(sizes are relative; none is estimated in hours)*

> **Precondition, owned by another lane and not filed here:** `Q-PROMISE-03`'s copy edit
> (`booking_success_t2_*`) must be complete on **both** mobile clients before R6/R8/R9 ship. Android is
> done; **iOS is not** (§D4.2). Identifiers R0 and R10 are retained as `WITHDRAWN` rather than reused, so
> R11's dependency arrow still parses and so nobody re-files them from an old checkout.

| # | Carries | Depends on |
|---|---|---|
| ~~**R0**~~ | **WITHDRAWN — shipped 2026-08-08** in an independent lane. `order.confirmed`'s copy is already corrected (`values/strings.xml:1211` = *"Booking confirmed ✅"*). **Do not re-file; do not "remove" the `Cleaner found! 🎉` string — at HEAD it belongs to `order.cleaner_assigned` and is correct there** (verify #13) | — |
| **R1** | The two `Order` columns + the widened `GrantPreferredHold` (**with D5.1's no-eviction invariant and the round increment**) + `PreferredOffer.StateOf` + `PreferredOfferInvariantTests`. ⚠️ `ef-migration` | — |
| **R2** | `DeclinePreferredOffer` + the shared `NotifyPreferredOfferClosedAsync` + D1.1's implicit decline in `TakeOrder.Handler` | R1 |
| **R3** | `NotifyLapsedPreferredOffers` + the timer + the tenancy shape + **the D6.1 digest regression test** | R1, R2 |
| **R4** | `ChoosePreferredCleaner` (customer) + the one error key × 5 locales. **Its validator carries all four refusals of §D5.1** — same id, already assigned, live reservation for someone else, `NotifyOnly`; **and the recurring refusal (§D6.2)** | R1 |
| **R5** | `GetMyPendingOffers` (partner + partner-mobile) with the D9 conjunct set | R1 |
| **R6** | The customer order-detail DTO block, **`canChooseAnother` computed through `BookingPolicy.ComputePreferredHold`, never a client constant**. ⚠️ `nswag-regen` | R1, R4 |
| **R7** | The three partner clients: the pending-offers surface + Confirm/Decline. **"Confirm" calls `TakeOrder`.** **No partner copy change** — the shipped `notification_preferred_offer_*` strings stay (§D10.1.1) | R5 + regen |
| **R8** | The three customer clients: state, respond-by, "choose another / any cleaner". ⚠️ **the row-dropping mapper pin per client**. ⚠️ **the D10.3 copy constraint — a promise to FIND, no name, no outcome**. ⚠️ **§D4.2 — no absolute time-to-assignment anywhere** | R6 + regen |
| **R9** | The affirmative copy wave — five locales, all clients — now that the promise is decided. Feeds T-0491 / T-0544. **Bounded by §D4.2's standing constraint** | R1 |
| ~~**R10**~~ | **WITHDRAWN — shipped 2026-08-08.** The key, its category arm, the shared notifier, both producers, the FCM arg map and the card-path test all exist (§D10.2) | — |
| **R11 — clients** | Push + feed templates for **`order.preferred_offer_closed` only**, 5 locales × 3 customer apps, **and only then** that one key is added to `NotificationFeedEventKeys.Customer` — the badge counts every row in the keyset (`NotificationFeedEventKeys.cs:26-28`). `order.cleaner_assigned` is already listed (`:35`) and already rendered on both clients. **No dependency on R10** | R3, R4 |
| **F1** *(filed, not built)* | `CanBrowseOrderAsync` omits the offerability conjunct (D9 / D10.1.1) | — |
| **F2** *(filed, not built)* | The web customer wizard has no preferred-cleaner picker at all | — |
| **F3** *(filed, not built)* | A per-template preferred-cleaner surface — what would let D6.2's suppression be lifted **and** give a recurring customer the exit §D6.2 refuses on the occurrence | — |

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
- `patterns-backend.md` (or `consistency.md`) — a **new** rule, generalised from the shipped
  `order.cleaner_assigned` split (§D10.2) and stated so it is checkable rather than aspirational.
  **Descriptive of code that exists and ran**, not a prescription for a stack this ADR never built:
  > **A notification key means ONE fact. If the column it is produced from is overloaded, the key must
  > not be.** `Order.CurrentStatus == Confirmed` means *money settled* **or** *a cleaner took it*
  > (`CLAUDE.md`, "never read it as 'a cleaner is on this job'"). A push key produced from all of its
  > writers therefore says something false for some of them. **Before adding a producer to an existing
  > key, read every other producer and ask whether the rendered string is true for each.** The check is
  > mechanical: for each `NotificationEventCatalog.X`, `git grep` its producers and read the client
  > string once. *Reference implementation:* `NotificationEventCatalog.cs:26-37` +
  > `OrderCleanerAssignedNotifier.cs:17-41`, pinned by `OrderConfirmedHonestProducerTests.cs:66,92` and
  > `OrderCleanerAssignedNotificationTests.cs:93`.
- `patterns-backend.md` — a fourth bullet, from CH-F3:
  > **An invariant on a monotone quantity is not an invariant.** Before writing a guard of the form
  > *"the new value must exceed the stored one"*, differentiate: if the quantity is increasing in time,
  > the guard passes for every later call and refuses nothing. State the property you actually want
  > (*"no live reservation for someone else"*) on the column that expresses it.
- `agents/knowledge/roles/preferred-cleaner-hold-resolver.md` — the resolver's "does NOT know" list
  gains: *the round count, the notification receipt, and whether anyone has been told* (it stays a pure
  read; the aggregate owns the counter and the notifier owns the receipt).
- A new role card, `roles/preferred-offer-lapse-notifier.md`.
- `agents/architecture/decisions/preferred-cleaner-dispatch.md` — **rewritten in full** in the same
  change as acceptance. The lead already added a dated correction banner (2026-08-08) retiring that
  page's *"Nothing is shipped yet"*, its *"Consumption — None"* row and its *"No membership check"* row;
  the rewrite folds this ADR's shape in and **removes the superseded dated snapshot rather than leaving
  it under a warning**. That page saying something false is the documented proximate cause of CH-F1.

---

## Owner rulings — all FIVE escalations ANSWERED 2026-08-08, and where each landed

**Zero escalations remain open in this ADR.** Each answer is recorded here with the section that
executes it, so a later reader does not re-derive a settled question.

| Question | Owner's answer | Where it landed | Reopened by a later reading? |
|---|---|---|---|
| **`Q-ASSIGN-01`** — for the terminal step, does the customer see a **name** or a **promise**? | **A promise, not a name.** The customer is told the platform will find them a cleaner; no specific person is shown. | **D5.2** — a binding, checkable copy constraint (*what Cleansia is doing*, never an outcome or a person), and **D10.3** applies the same rule to the *"a random is assigned"* sentence. Verify #14. | **no** |
| **`Q-ASSIGN-02`** — Invariant H 90% → 80%; is two rounds the right price? | **Keep two rounds.** The derived cap stands. | **D5.3** — `MaxPreferredOfferRounds = 2`, pinned against `MinimumOpenBoardShare` by `PreferredOfferInvariantTests`. Verify #8. The panel re-derived the cost independently at a true **19%**; the answer is unaffected. | **no** |
| **`Q-ASSIGN-03`** — does a cleaner who repeatedly declines lose the perk? | **Not now.** Declines stay unrecorded, nothing enforced. | **D13** — a full section, not a table row, because *the successor is a BUILD, not a config flip*: nothing records a decline, so any future policy needs A8's child table first and can never be applied retroactively. | **no** |
| **`Q-ASSIGN-04`** — is the *true* assignment wanted, or the reservation? | **The reservation** — *"They either **have to confirm**…"* — plus a new sentence adding a customer-facing confirmation message and a terminal state. | **§Context** (the shape argument), **D10.2** (the confirmation message — which shipped independently), **D10.3** (the terminal state). | **no** |
| **`Q-PROMISE-03`** — the 1-hour assignment promise vs. two rounds of up to 12 h: which gives? | **(c) — drop the one-hour number.** The post-booking screen stops stating an absolute time. **Two rounds are explicitly unaffected.** | **D4.2** — the dependency, the arithmetic (`0.10 × lead > 1 h ⟺ lead > 10 h`), the reason a *share* cannot bound an *absolute*, the **standing constraint** (*no surface may state an absolute time-to-assignment*), and the half-landed state of the copy edit. Verify #15. | **no — and reinstating a number reopens BOTH this ADR and `Q-PROMISE-03`** |

**What `Q-ASSIGN-04`'s answer changed in this draft, stated plainly:** it did not change the
mechanism — the reservation was already what the first draft built — but it added a **customer-facing
confirmation message**, and the first draft designed that message rather than checking for it. **It was
already built** (§D10.2, CH-F1). What the checking instruction was worth is therefore the opposite of
what the first draft claimed: the lesson is not *"we found a defect by checking"* but *"a section was
written against a living page that said nothing was shipped, and the panel is what caught it."* §Context
carries that framing at the top, because an ADR is read long after the panel that produced it.

**What `Q-PROMISE-03` changed:** nothing in the mechanism, and that is precisely why it is recorded as a
**dependency** rather than a design input. The reservation was already what it is; what moved was a
sentence on a screen, and this design is only safe because it moved.

---

## PM ruling, 2026-08-08 — the fifth term on `canChooseAnother`

The transcription pass surfaced one interaction and correctly declined to decide it: V4·21 fixes
`canChooseAnother` at **four** terms, and V8·36 makes `ChoosePreferredCleaner` **refuse a recurring
occurrence** — but recurring is not one of the four. So the flag reads `true` on a surface where the
server refuses.

**Ruling: add the fifth term (`RecurringTemplateId == null`).**

The lead's four-term list is not being overridden — it **predates** V8·36. The two items were ruled in
the same pass and their interaction was not in front of the lead, which is exactly the case a
transcription author is right to report rather than resolve.

The answer is forced by the ADR's own finding. **CH-F4 is the finding that a flag reads `true` where
the mechanism cannot deliver**, and it was ruled blocking. Shipping a second, known instance of the
same defect in the same release — while citing the first as blocking — is not a position this document
can hold. The flag is the validator's read side; where the two disagree, the flag is wrong by
definition.

Verify #17 is widened to assert all five terms. If the lead disagrees, this is the one line to revisit
before acceptance — it changes no mechanism, only whether a button is offered.

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
- **The unfilled-at-cleaning-time rate is unmeasured, and D10.3's copy is the only thing standing
  between the ladder and a promise the platform cannot keep.** Nothing in dispatch can guarantee a
  cleaner; only an operational commitment can. **Offered to the PM as a non-blocking owner question
  rather than filed here** (I do not hold `questions/open.md`), in this shape: *"the flow now ends with
  'we're finding you a cleaner'. Some orders reach their cleaning time with nobody on them — that is
  true today and this feature does not change it, but it does now arrive at the end of a conversation
  we started. Do you want an operational fallback (admin dispatch / a staffed cleaner) at that point,
  or is 'we're still looking' the final state?"* **No default taken** — a fallback is a business
  capability, not an architecture, and building either answer's copy before it is settled is a guess.
- **The `Q-PROMISE-03` copy edit is half landed** (§D4.2): Android's five locales carry no absolute
  duration, **so do iOS's — landed 2026-08-08 in the same wording, byte-identical after unescaping.**
  **Verify #15 passes on both platforms** and R6/R8/R9's precondition is met. The FOURTH step's
  *"we'll remind you 1 hour before"* is deliberately NOT covered by either guard — it is `Q-PROMISE-04`,
  a promise the platform could keep but does not implement, and the likely fix is to build the reminder.
- **`Q-PROMISE-04`** — the same post-booking screen's *"We'll remind you 1 hour before"* — is open and is
  **not** governed by §D4.2's constraint (which is scoped to time-to-**assignment**). Same shape, same
  lane, different promise. Recorded so nobody folds it into this ADR or assumes §D4.2 already answers it.
- **The timing side channel §D7.3 concedes is not closed**, deliberately, and its closing move is
  recorded there (announce both outcomes at the deadline — a change to the notifier's caller). It becomes
  a decision again only if `Q-AVAIL-04` resolves against any disclosure.
- ⚠️ **One term is missing from `canChooseAnother` and the author did not add it, because the panel did
  not rule it.** §D5.1 refuses `ChoosePreferredCleaner` on a recurring occurrence (§D6.2, the panel's
  CH-F8 ruling), but §D7.2's flag — specified by the panel as exactly four terms — has **no**
  `RecurringTemplateId == null` term. **That is CH-F4's defect, in miniature, for recurring customers:
  a flag reading `true` where the server will refuse.** Adding the term is one conjunct and looks
  obviously right; it is left open because the flag's term list was ruled explicitly and this ADR's
  author is not the one to widen a ruled list. **Whoever accepts this ADR should either add the fifth
  term or record why four is correct** — it is the only known internal disagreement left between §D5.1
  and §D7.2.

---

## Challenge

**One challenger, single lane (`mechanism + promise`), 2026-08-08. Full text:**
`agents/backlog/adr/challenges/NNNN-favourite-cleaner-reservation.md` — reviewed against HEAD, nine
findings, five declared blocking. Headlines, so this ADR carries its own trail:

| # | Finding | Declared |
|---|---|---|
| **CH-F1** | §D10.2 (`order.cleaner_assigned`, its producers, its copy correction) describes work already shipped; R0/R10 re-do it and **verify #14 would delete correct copy** | blocking |
| **CH-F2** | The reservation contradicts `Q-PROMISE-01` (*"a cleaner is assigned within 1 hour"*, copy live); any lead > 10 h breaks it on round 1 alone, and Invariant H is a *share* that cannot bound an *absolute* figure | blocking |
| **CH-F3** | `GrantPreferredHold`'s re-grant invariant *"`untilUtc` > the current value"* is **vacuous by monotonicity** — it permits the silent eviction it names, and admits a zero-length reservation that burns the customer's last round | blocking |
| **CH-F4** | `canChooseAnother` omits the lead-time term; the 8-hour floor makes the exit unreachable over 37–100% of the post-lapse window, and it reads `true` for the whole 2–8 h notify-only band | blocking |
| **CH-F5** | §D7.3's neutrality is defeated by §D6.4 (immediate decline) + §D7.2 (disclosed `respondByUtc`): the **arrival time** discloses decline-vs-silence | blocking |
| **CH-F6** | The targeted push fires wider than the reservation (notify-only band, pre-webhook card orders), its shipped partner copy is priority framing, and the deadline has no wire path — none of it is in the ticket bill | non-blocking |
| **CH-F7** | The two-round derivation is sound (true max **19%**), but §D5.3's stated worst-case *pairing* (24 h / 120 h) is wrong — 24 h needs `L ≥ 132`; at 120 h it is 22 h 48 | non-blocking |
| **CH-F8** | `ChoosePreferredCleaner` on a recurring occurrence is unruled: it works, is per-occurrence, and silently reverts next week | non-blocking |
| **CH-F9** | *"Composes with ADR-0035 — §D8 is where the two perks collide"* is false by construction: express (lead ∈ [2,4)) and the reservation (lead ≥ 8 h) are **disjoint** | non-blocking |

**Checked and found sound (not to be re-litigated):** §D2's three legs, `OrderVisibility`/`OrderAvailability`
as described, §D6.1's digest-freshness regression argument, §D9's `CanBrowseOrderAsync` ⚠️, §D3/A13's
*"the confirm is `TakeOrder`"* walked gate by gate, and the non-existence of the two new columns.

## Defense

*(No author round was run. The PM collapsed the loop and had the lead adjudicate directly, because the
strongest finding — CH-F1 — has an external cause the author could not have answered: see §Verdict
"Why CH-F1 is not an author failure". Every finding is ruled in §Verdict; each ruling is derived from a
file the lead opened at HEAD, not from either document's description of it.)*

**The author's role in this round was therefore EXECUTION, not defense: all nine findings were conceded
as ruled** — five blocking, four not — **and the lead's 39-item edit list was applied in full.** Nothing
was rebutted; nothing was escalated. The one thing the author added beyond the list is a **disclosure,
not a decision**: §Open records that §D5.1's recurring refusal has no counterpart term in §D7.2's ruled
four-term flag. See §Revision log for the item-by-item map and for what the citation re-anchoring found.

## Verdict

**Lead, `architect` in lead mode, 2026-08-08. Consensus NOT declared. Five findings stand; the ADR is
REVISED, not accepted.** Zero escalations: the one genuine collision was escalated ahead of this round
and is **answered** (`Q-PROMISE-03`). **No schema shape moves** — the two proposed columns
(`PreferredOfferRound`, `PreferredOfferLapseNotifiedAt`) survive every ruling below unchanged, and no
ruling adds a column, an index, an arg on the wire or a status.

### Per-finding ruling

| # | Ruling | One-line reason |
|---|---|---|
| **CH-F1** | **STANDS** (author not at fault) | Verified at HEAD: the key (`NotificationEventCatalog.cs:37`), its `OrderUpdates` arm (`:89`), the shared notifier (`OrderCleanerAssignedNotifier.cs:17-41`), both producers (`TakeOrder.cs:275-276` **outside** the `statusChanged` guard at `:267-273`; `AdminReassignOrder.cs:102-103`), the FCM arg map (`FcmMessageFactory.cs:33`), the corrected copy (`values/strings.xml:1211` = *"Booking confirmed ✅"*) and the tests (`OrderCleanerAssignedNotificationTests.cs:70,89,93,128,152`) **all exist**. |
| **CH-F2** | **STANDS — and is DISCHARGED by an owner ruling that post-dates it, not by the design** | The arithmetic is right (`0.10 × lead > 1 h ⟺ lead > 10 h`, from `BookingPolicy.cs:159`/`:160`/`:171-180`). `Q-PROMISE-03` (owner, 2026-08-08, `questions/open.md:1981-1997`) rules **(c) drop the one-hour number**; two rounds are explicitly unaffected. The ADR must now **record the dependency**, not argue the point. |
| **CH-F3** | **STANDS** | Re-derived: `untilUtc(t) = t + min(0.10·(L − t), 12)` has slope 0.9 below the ceiling and 1.0 at it, so at `L = 24 h` a re-grant three minutes in yields **2.445 h** against a stored **2.4 h** and passes. The guard refuses nothing in the grantable domain. |
| **CH-F4** | **STANDS** | `ComputePreferredHold` returns `Zero` below 8 h (`BookingPolicy.cs:174`) → `NotifyOnly` (`PreferredCleanerHoldResolver.cs:94-98`), so the two-term flag is `true` where the server must refuse. Dead-tail shares re-derived: **100% / 74% / 37% / 7%** at 8 / 12 / 24 / 120 h. |
| **CH-F5** | **STANDS — conceded, not closed** | Both disclosures are in this document (§D6.4 immediate, §D7.2 `respondByUtc`), so the bit is recoverable. §D7.3 claims a property it does not have; the property it *does* have is worth stating and is enough for `Q-AVAIL-04`. |
| **CH-F6** | **STANDS (non-blocking); ruled on the merits, and the ADR's answer is "no wave" — deliberately** | Verified: `OrderFactory.cs:184` grants on the deadline, `:192` pushes on the recipient (its own comment names the wider predicate); args are `orderId` + `orderNumber` only (`:197-201`); partner copy `partner-app/…/values/strings.xml:1244-1245`. |
| **CH-F7** | **STANDS on the pairing; the DERIVATION SURVIVES** | Independently re-derived: max withheld share is **19%** (`0.19L` for `L ≤ 120`, falling above it); **24 h needs `L ≥ 132`** (18.2%); at `L = 120` the worst case is **22 h 48**. The invariant test is a conservative over-estimate — the right direction. |
| **CH-F8** | **STANDS** | `MaterializeRecurringBookingTemplate.cs:240` really does carry `template.PreferredEmployeeId` into every occurrence; §D6.2 suppresses the prompt and nothing suppresses the action. |
| **CH-F9** | **STANDS** | Express is lead ∈ [2, 4) (`BookingPolicy.cs:130-140`, `:20`, `:26`); a hold needs lead ≥ 8 h (`:174`). Disjoint. The composition claim is false; the **non**-interaction is the fact worth recording. |

**One correction against the challenger, in the same direction the lead was warned about** (*right about
the mechanism, wrong about the magnitude*): CH-F1's proposed remedy — *"`NotificationFeedEventKeys.Customer`
still does not list `OrderCleanerAssigned`… that is the only survivor of R0+R10"* — is **also already
done**. `NotificationFeedEventKeys.cs:35` lists it, `NotificationFeedEventKeysTests.cs:63` pins it, and
both clients render it (`CustomerFeedEventKeys.kt:18`, `CustomerFeedEventKeys.swift:9`,
`NotificationTemplates.kt:29-30`, `push.order.cleaner_assigned.title/body` at
`Localizable.xcstrings:24119`, five locales). **The residual of R0 + R10 + half of R11 is ZERO, not one
ticket.**

### Why CH-F1 is not an author failure, recorded so the ADR does not read as carelessness

The corrective §D10.2 designs was **dispatched as an independent lane while this draft was being
revised**, and the author was not told; they re-read a pre-fix file. The evidence is arithmetic: the
draft cites the four partner constants at `NotificationEventCatalog.cs:30/:44/:52/:60`; HEAD has them at
`:43/:57/:65/:73` — **every one off by exactly 13**, the size of the `OrderCleanerAssigned` block
(`:26-37`) plus its blank line. Every citation the lead spot-checked *outside* that file is either exact
(`Order.cs:424-435`, `:438-443`, `:689`; `CancellationAssessor.cs:55`; `BookingPolicy.cs:159`, `:160`,
`:171-180`, `:252-255`; `OrderRepository.cs:245-257`, `:282-284`, `:318-333`;
`MaterializeRecurringBookingTemplate.cs:240`; `OrderVisibility.cs:36-52`) or off by one from ordinary
drift. **The census was stale, not sloppy — but it is still stale, and one verification step built on it
would delete correct customer copy in five locales on two clients.** That is why CH-F1 blocks.

### The three durable decisions this round adds (the part that survives the edit list)

1. **A re-grant may not evict a live beneficiary — and the invariant that says so must key on the HOLD,
   not on the preference column.** `Order.Create` writes `PreferredEmployeeId` at `Order.cs:387`
   independently of any hold, so the customer's stored pick is non-null in the notify-only band and in
   every declined outcome. An invariant phrased on `PreferredEmployeeId` would refuse re-offers that
   never held anything and permit ones that do.
2. **The customer's exit is a long-lead affordance, and the ADR discloses its own dead tail.** The
   8-hour floor is not a bug in the exit; it is the honest bound of it. A flag that hides the bound is
   worse than a feature that has one.
3. **Neutrality here is MINIMIZATION, not non-inferability.** The platform makes no statement about a
   worker; it does not, and this ADR must not claim it does, prevent a customer inferring *"the
   reservation ended early"* from a phone buzzing. Stating the weaker true property is what keeps the
   `Q-AVAIL-04` posture defensible.

### Closed edit list — EXECUTED 2026-08-08 by the author; see §Revision log

*(The lead's 39-item list was transcription-only and has been applied in full. Per the lead's own
instruction it is not carried forward as a live spec — the per-finding rulings above are the record, the
§Revision log below maps every item to the section that now carries it, and the list's text remains in
this file's history.)*

### What is unchanged, and is not to be reopened

§D2 (no `OrderEmployee` row) — the lead re-derived all three legs at HEAD independently of both
documents: `OrderRepository.GetEmployeeOrderCountThisWeekAsync` (`:245-257`) counts
`AssignedEmployees.Any(...)` over the UTC week with **no status term and no confirmation term**;
`LiveCommitmentsInWindow` (`:318-333`) is the one overlap predicate, read by both the `TakeOrder`
conflict gate (`:282-284`) and ADR-0039's picker (`:302-307`); `CancellationAssessor.cs:55` is verbatim
`var hasBeenAccepted = order.AssignedEmployees.Count > 0;`, driving `ClassifyCancellation`'s
`FreeNotAccepted` arm (`BookingPolicy.cs:252-255`). **All three hold. A1's rejection is correctly
grounded and the load-bearing decision stands.** Also unchanged: the four owner rulings
(`Q-ASSIGN-01…04`), the 8-hour floor, the 12-hour ceiling, `MaxPreferredOfferRounds = 2`, the
reservation-over-true-assignment choice, `TakeOrder` as the confirm (A13), and the two-round derivation
(**19% actual against a 20% bound — conservative in the safe direction; settled, do not re-derive**).

**Re-verified by the author at HEAD on 2026-08-08, independently of the lead** *(because "still true" is
the only part of a stale-citation finding that cannot be inherited)*: all three legs hold, at the same
lines — `OrderRepository.cs:245-257`, `:318-333` (read by `:282-284` and `:302-307`),
`CancellationAssessor.cs:55` → `BookingPolicy.cs:252-255`. And the three artifacts this ADR proposes
still do not exist anywhere under `src/`: `PreferredOfferRound`, `PreferredOfferLapseNotifiedAt`,
`MaxPreferredOfferRounds` (nor `MinimumOpenBoardShare`, `DeclinePreferredOffer`,
`ChoosePreferredCleaner`, `order.preferred_offer_closed`).

---

## Revision log — the lead's V1–V10 (39 items), and where each landed

**Executed 2026-08-08 by the `architect` in author mode. Transcription only: no ruling was reopened, no
alternative added beyond the two the lead ordered (A15, A16), and nothing was decided that the lead had
not already decided.** The status stays `proposed` and the file stays in `drafts/` — the author does not
accept their own ADR.

| Item | Ordered | Landed in |
|---|---|---|
| **V1 · 1** | Re-anchor the §Context partner table | §Context — `:43` / `:57` / `:65` / `:73`, `NotificationFeedEventKeys.cs:48-54` |
| **V1 · 2** | Replace the "something worse than a gap" sentence | §Context, final paragraph of the notification census |
| **V1 · 3** | §D10.2 → a citation list, not a ruling | §D10.2, retitled *"a PRECONDITION ALREADY MET"*, 12-row evidence table |
| **V1 · 4** | §D10.2 keeps exactly one ruling | §D10.2's blockquote — `order.preferred_offer_closed`'s shape + the sequencing rule |
| **V1 · 5** | §D10.3 table row → shipped | §D10.3 customer-events table |
| **V1 · 6** | Delete the "strictly bigger win" bullet; two events → one | §Consequences (Positive + Negative both re-totalled) |
| **V1 · 7** | Delete old verify #13 | deleted |
| **V1 · 8** | Invert old verify #14 | **new verify #13** — *"THIS ADR REMOVES NO CUSTOMER COPY"*, with both clients' current line numbers |
| **V1 · 9** | Delete old verify #15 | deleted |
| **V1 · 10** | R0/R10 `WITHDRAWN`; R11 narrowed | ticket table; R11's depends-on is now R3, R4 |
| **V1 · 11** | Record *why* the census was stale | §Context's opening blockquote (dated, with the exactly-13-line arithmetic) |
| **V2 · 12** | Front-matter gains `Q-PROMISE-03` | front-matter "Owner rulings this ADR carries" |
| **V2 · 13** | New §D4.2 | §D4.2, incl. the **standing constraint** and the half-landed state of the copy edit |
| **V2 · 14** | New verify step | **verify #15** |
| **V3 · 15** | Two invariants replace the vacuous one | §D5.1's `GrantPreferredHold` block |
| **V3 · 16** | The refutation, inline | §D5.1 blockquote (monotonicity, 2.445 h vs 2.4 h) |
| **V3 · 17** | Keys on the HOLD, not the preference | same blockquote, second paragraph |
| **V3 · 18** | The product consequence | §D5.1, *"the customer's second choice becomes available when the reservation ENDS"* |
| **V3 · 19** | New **A15** | Alternatives table |
| **V3 · 20** | New verify step | **verify #16** |
| **V4 · 21** | `canChooseAnother` gains two terms + the sentence | §D7.2's DTO block + the paragraph under it |
| **V4 · 22** | §D5.1 rules `NotifyOnly` → refuse | §D5.1, refusal (4) |
| **V4 · 23** | §D7.1 amendment | §D7.1 blockquote |
| **V4 · 24** | The round-counter sentence | §D5.3, above Invariant H |
| **V4 · 25** | The dead-tail table | §D7.2 |
| **V4 · 26** | New verify step | **verify #17** — the short-lead case as ordered; V8's recurring case is listed beside it as a *separate* assertion on the command only, because the panel ruled the flag's terms explicitly and recurring is not one of them (§Open) |
| **V5 · 27** | The conceded row | §D7.3 blockquote, ahead of its three grounds |
| **V5 · 28** | Its three reasons | same blockquote |
| **V5 · 29** | New verify step | **verify #18** |
| **V6 · 30** | Push keeps the wider predicate and its copy; no partner wave | **new §D10.1.1** |
| **V6 · 31** | Deep link stays on order detail | §D10.1.1 |
| **V6 · 32** | Push-before-offerable accepted and bounded | §D10.1.1 |
| **V6 · 33** | New **A16** | Alternatives table |
| **V7 · 34** | The 19% correction and the `L ≥ 132` pairing | §D5.3 |
| **V7 · 35** | "Conservative over-estimate; not tight at equality" | §D5.3 + **verify #8** |
| **V8 · 36** | `ChoosePreferredCleaner` refused on recurring | §D6.2 (+ §D5.1 refusal list, + R4) |
| **V9 · 37** | Composition line | front-matter |
| **V10 · 38** | Re-anchor every `file:line` | throughout — see the note below |
| **V10 · 39** | Re-total §Consequences and the ticket table | §Consequences, ticket table |

**On V10 · 38, stated precisely, because the finding it answers was about exactly this.** Every
`file:line` retained in this document was re-opened at HEAD by the author on 2026-08-08. The eight
files the lead flagged as unchecked were all opened: `NewJobsDigestService`, `OrderAccessService`,
`GetMyServingCleaners`, `CleanupStalePendingOrders`, `CancelOrder`, `CreateOrder`,
`HandlePaymentNotification`, `ConfirmRecurringOrder` — **all eight were correct as cited** and needed
no change. Corrected: the four `NotificationEventCatalog` constants (+13), `NotificationFeedEventKeys`
(`:47-53`→`:48-54`, `:26-29`→`:26-28`), `TakeOrder` (`:46-71`→`:45-70`, `:83-91`→`:82-90`,
`:265`→`:264`, `:200-207`→`:199-208`, `:70-71`→`:69-70`, `:212-228`→`:211-227`, `:56-57`→`:55-56`),
`AdminReassignOrder` (`:86`→`:88`, `:98`→`:100`), `PreferredCleanerHoldResolver` (`:95-98`→`:94-98`),
`OrderFactory` (`:175-202`→`:175-205`, `:192-202`→`:192-205`, `:194`→`:196`), `OrderMappers`
(`:94`→`:94-95`), `order-wizard.facade.ts` (`:576-580`→`:575-577`), and the iOS
`CustomerFeedEventKeys.swift:9` → `CleansiaCustomer/Sources/Features/Notifications/NotificationFeedTemplates.swift:9`
(the enum is real and the line is right; the file name was not). Deleted rather than re-anchored:
`NotificationsInboxSheet.swift` as a *"Cleaner found"* site, and the whole *"three producers of
`order.confirmed`"* table — both belonged to work that shipped.
