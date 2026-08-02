# Preferred-cleaner dispatch — living decision notes

> **Status of this page: TRACKING A PROPOSED ADR.** The current shape below is what
> **[ADR-0036](../../backlog/adr/0036-preferred-cleaner-first-refusal-hold.md)** proposes; it is
> `proposed`, not adjudicated. **Nothing here is enforceable yet** — until the panel accepts it, the
> shipped behaviour is the "Today" section, and `agents/knowledge/patterns-backend.md` carries no rule
> about holds.
>
> Companion pages: [`membership-benefits.md`](./membership-benefits.md) (ADR-0035 — the express waiver
> this collides with), [`push-notifications.md`](./push-notifications.md) (ADR-0025 — the display
> contract the targeted push rides), [`outbox.md`](./outbox.md) (ADR-0002/0008).
> Business view: `agents/analysts/notifications.md`. Published view: `docs/architecture/backend.md`.

---

## Today (shipped, `master` / `docs/sprint-15-decisions`, verified 2026-08-02)

**The customer can express a preference. The platform does nothing with it.**

| Layer | State |
|---|---|
| Capture | iOS `ConfirmStep.swift:77,198`; Android `ConfirmStep.kt:362-363` + `PreferredCleanerPicker.kt`. **The web wizard has no picker** — `order-wizard.facade.ts:580` sends `undefined` unconditionally. |
| Picker source | `GetMyServingCleaners` — cleaners on the customer's `CurrentStatus == Completed` orders, top 20 by most recent. |
| Validation | `CreateOrder.cs:140-154` → `OrderRepository.UserHasCompletedOrderWithEmployeeAsync` (`:294-305`). **One rule: a completed order with that cleaner. No membership check** — so a non-member can set it today, while all three clients advertise it as a Plus perk. |
| Persistence | `OrderFactory.cs:124` → `Order.cs:349`. Nulled by `AnonymizeCustomerData` (`:621`). |
| **Consumption** | **None.** No query, no ordering, no notification, no assignment reads `PreferredEmployeeId`. |
| Dispatch | `TakeOrder.cs` — first-come-first-served off a pull board; six gates (spots, caller-is-employee, address, `Approved`, not-already-assigned, rating-tiered weekly limit, time conflict) and **zero** mention of the preference. |
| Recurring | `MaterializeRecurringBookings.cs:138` passes `null` — and `RecurringBookingTemplate` **has no field to pass**. The preference was never modelled on a schedule. |
| Docs | `Order.cs:217-224` describes *"the matching algorithm boosts this employee's score"* and *"no UI sets it"*. **Both halves false.** Correction text is in ADR-0036 §Naming; lands in T-0515. |

**Owner rulings, 2026-08-02:** *"It exists… I'd like to have it working fully"* (so *withdraw the claim*
is dead) and, on `Q-PLUS-03`, **"plus-only"**.

---

## The shape being proposed (ADR-0036)

**First refusal, not priority.** For a bounded interval the order is withheld from the board and only
the preferred cleaner can see or take it; then it opens to everyone, unchanged.

```
Order.PreferredEmployeeId    — what the customer ASKED FOR   (durable fact, already exists)
Order.PreferredHoldUntilUtc  — what the platform GRANTED     (policy outcome, new, nullable)
```

Two columns, two lifetimes, on purpose: "we stored your preference but could not act on it" has to be
expressible, and the visibility predicate must key on the **deadline** so no legacy row acquires
behaviour.

### The window

```
hold = 0                                   when lead < BookingPolicy.StandardLeadTimeHours (4)
     = min(lead × 0.10, 12h)               otherwise
```

| Lead | Hold | Open to everyone |
|---|---|---|
| 2–4 h (express band) | **0** | 100% |
| 4 h | 24 min | 90% |
| 24 h | 2 h 24 | 90% |
| 168 h (recurring) | 12 h (ceiling) | 93% |

**Invariant H — the whole safety argument in one line:** *at least 90% of every order's fill window is
always open to the entire board.* The hold can never be why an order went unfilled.

### The expiry has no actor

`now >= PreferredHoldUntilUtc` is a `WHERE` clause. **No job, no sweep, no outbox message, no status
transition, no row change.** The failure mode of a job-driven expiry is *an order stuck held*; a clock
comparison has no failure mode of that shape. This is the single property everything else hangs off.

### Where it is enforced — five surfaces, one expression

`OrderVisibility.NotHeldFromEmployee(employeeId, nowUtc)` in the Domain, applied at:

1. `OrderSpecification` (serves `GetPagedOrders.cs:91` **and** `GetAvailableJobsPreview.cs:50`)
2. `OrderAccessService.CanBrowseOrderAsync` — the detail page
3. `NewJobsDigestService.cs:98-114` — a **hand-rolled** predicate that does not use the specification
4. `TakeOrder.Validator` — the write gate

**A rule applied to n−1 of n surfaces is a leak.** This sprawl pre-dates the ADR; the change is the
first thing that forces it to converge.

### The non-obvious one: the digest watermark (ADR-0036 D5.3)

`NewJobsDigestService` decides "new to this cleaner" by comparing the latest `OrderStatusTrack.CreatedOn`
against `Employee.LastNewJobsDigestAt`. **If a hold hides an order for 45 minutes, at expiry its status
track is older than every cleaner's watermark and the order is never digested again** — it becomes
board-only, forever. Fix: for a non-preferred cleaner the availability instant is
`max(latest status-track CreatedOn, PreferredHoldUntilUtc)`. One expression, no new column.

> **Latent, independent of this ADR:** the overlap filter at `:137-142` has the same shape — an order
> skipped for a time conflict that later clears is also never re-notified. Flagged in the ADR's defense
> (CH-4) for the PM to file separately.

### Notification and the privacy line

- New event `order.preferred_offer`, produced inline in the create path, **bypassing the 30-minute
  digest cadence** and **not** stamping the watermark. Category: `NewJobsAvailable` (so the existing
  mute governs it).
- **No notification ⇒ no hold.** A muted cleaner gets no hold at all — latency with no signal is pure
  loss.
- **`Order.cs:221-222`'s "not exposed to the cleaner side" is kept for everyone who was *not* chosen and
  deliberately dropped for the one who was.** Exclusivity is invisible to the excluded by construction
  (a board is a query result, not a diff), which is exactly what that rule was protecting.
- The take-time refusal is the **existing `OrderNotFound`** — because *the error must agree with what
  that caller's GET returns*. A bespoke "held for another cleaner" error would leak the thing the rule
  hides.

### The customer sees nothing

No countdown, no "waiting for Anna", no push on expiry. The perk is honoured invisibly or not at all —
which is what makes the latency acceptable. The customer is told **once, at the moment of choosing**,
including the fallback: *"if they can't take it we open it to everyone right away, and your cleaning
time doesn't change."*

### The Plus gate

- Server-side, a **second `MustAsync`** in the existing `When(...)` block at `CreateOrder.cs:140-147`,
  using `IUserMembershipRepository.GetActiveForUserNoTrackingAsync` — the one live-membership predicate
  (same one `CancellationPolicyResolver`, `OrderFactory`, `QuoteOrder` and `CreateRecurringBooking` use).
- **Reject, do not silently ignore** — the same field already fails the whole order when the preference
  is ineligible (`CreateOrder.cs:143-146`).
- **Existing non-member orders are left alone** and are inert by construction (their
  `PreferredHoldUntilUtc` is `null`). **No backfill.**
- **A member who lapses keeps the hold on orders already created** (ADR-0009 D2 / ADR-0035 D1's freeze).
- **Recurring degrades instead of rejecting** — *reject where a human can react; degrade where nobody
  can*. A 03:00 sweep must never drop a customer's cleaning because a subscription lapsed.

---

## Trade-off space (the map, kept current)

| Axis | Chosen | Live alternative | What would flip it |
|---|---|---|---|
| Mechanism | exclusive hold | **board ordering / boost** (A1) | evidence that fill time is already marginal — but the first response should be lowering the fraction, not switching |
| Window | proportional + ceiling | fixed duration (A3) | a challenger showing the proportional form is unexplainable to a customer (it is never *told* to a customer, which is why it can be a formula) |
| Expiry | clock comparison | job / status transition (A5) | nothing — this one is not close |
| Storage | stored deadline | duration read at query time (A4) | nothing — A4 retroactively activates every legacy row |
| Hold floor | `StandardLeadTimeHours` (4) | **`2 ×` it (8)** — pre-flagged as a candidate blocking amendment | a challenger sizing the 4h-lead worst case with a thin cleaner pool |
| Non-member with a preference | reject | accept-and-ignore (A10) | a lead ruling that revenue beats consistency; mitigated by shipping the client gate a release earlier |
| Eligibility rule | keep "completed order with them" | drop it (A12) | nothing — dropping it makes the perk a customer-controlled targeting primitive |

## Open / undecided

- **The constants are uncalibrated.** `0.10` and `12h` are reasoned, not measured; DEV is live and was
  not queried. Both are single constants that can be tuned without touching a live order or the schema —
  which is precisely why the stored-deadline shape was chosen.
- **`CanBrowseOrderAsync` evaluates the shared rule in memory**, not as a composed expression. Shape
  shared, evaluation not. Enforceable only by review + one test assertion.
- **No `EXPLAIN` was run.** Whether `PreferredHoldUntilUtc` wants a partial index, and whether D5.3's
  `max(...)` inside the digest's per-cleaner loop regresses the sweep, is unanswered.
- **An order returning to the board after a cleaner cancels** (`order.assignment_cancelled`) gets **no
  new hold** under the current proposal — not examined, arguably wrong.
- **A fallback list** (second-choice cleaner) — not considered.
- **Admin visibility of a live hold** — not decided.

## Consumers

| Ticket | Carries |
|---|---|
| **T-0515** | the hold: column (⚠️ `ef-migration`), `ComputePreferredHold`, the resolver, the shared expression at all five surfaces, **D5.3's digest fix**, the targeted push, the `Order.cs:217-224` correction |
| **T-0516** | the Plus gate (`Q-PLUS-03` **answered: plus-only**) |
| *new, PM to file* | recurring carry-through — `RecurringBookingTemplate.PreferredEmployeeId` (⚠️ second `ef-migration`, ⚠️ `nswag-regen`) + the degrade rule |
| *new, PM to file* | the web wizard has no preferred-cleaner picker at all |
| *new, PM to file* | the digest's overlap-filter variant of the watermark defect (pre-existing) |
| **T-0491** | the copy — constrained by ADR-0036 §Copy, not decided by it |
