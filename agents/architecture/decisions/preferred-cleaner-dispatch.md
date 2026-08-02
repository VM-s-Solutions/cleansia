# Preferred-cleaner dispatch — living decision notes

> **Status of this page: CURRENT SHAPE.** **[ADR-0036](../../backlog/adr/0036-preferred-cleaner-first-refusal-hold.md)**
> is **`accepted`** (2026-08-02, after a full defense panel: author + three challengers + lead). The ADR
> is the immutable record and carries the `## Challenge` / `## Defense` / `## Verdict` trail; **this page
> is the evolving companion and is what you read first.** `agents/knowledge/patterns-backend.md` now
> carries the enforceable rule (*"Bounded exclusivity on a pull board"*), and the role card is
> `agents/knowledge/roles/preferred-cleaner-hold-resolver.md`.
>
> **Nothing is shipped yet.** "Current shape" means *the decision is made*, not *the code exists* — see
> §Consumers and the three preconditions.
>
> Companion pages: [`membership-benefits.md`](./membership-benefits.md) (ADR-0035 — the express waiver
> this composes with), [`push-notifications.md`](./push-notifications.md) (ADR-0025 — the display
> contract the targeted push rides), [`outbox.md`](./outbox.md) (ADR-0002/0008).
> Business view: `agents/analysts/notifications.md`. Published view: `docs/architecture/backend.md`.

---

## Today (shipped, verified 2026-08-02)

**The customer can express a preference. The platform does nothing with it.**

| Layer | State |
|---|---|
| Capture | iOS `ConfirmStep.swift:77,198`; Android `ConfirmStep.kt:362-363` + `PreferredCleanerPicker.kt`. **The web wizard has no picker** — `order-wizard.facade.ts:576-580` sends `undefined` unconditionally. |
| Picker source | `GetMyServingCleaners` — cleaners on the customer's `CurrentStatus == Completed` orders, top 20 by most recent. |
| Validation | `CreateOrder.cs:140-154` → `OrderRepository.UserHasCompletedOrderWithEmployeeAsync` (`:294-305`). **One rule: a completed order with that cleaner. No membership check** — so a non-member can set it today, while all three clients advertise it as a Plus perk. |
| Persistence | `OrderFactory.cs:124` → `Order.cs:349`. Nulled by `AnonymizeCustomerData` (`:621`) — **one half of the pair only**, which is the defect ADR-0036 CH-V1 turned into a design change. |
| **Consumption** | **None.** No query, no ordering, no notification, no assignment reads `PreferredEmployeeId`. |
| Dispatch | `TakeOrder.cs` — first-come-first-served off a pull board; six gates and **zero** mention of the preference. |
| Recurring | `MaterializeRecurringBookings.cs:138` passes `null` — and `RecurringBookingTemplate` **has no field to pass**. |
| Copy | **Five live false statements**, catalogued by the panel — see §The copy debt below. This is the part that ships first. |

**Owner rulings, 2026-08-02:** *"It exists… I'd like to have it working fully"* (so *withdraw the claim*
is dead); `Q-PLUS-03` → **"plus-only"**; and **the hold floor is 8 hours** (CH-2, ruled by the owner
against the author's 4).

---

## The shape (ADR-0036, as amended by the panel)

**First refusal on the first seat, not priority.** While an order has **no cleaner on it yet**, it is
withheld from the board and only the preferred cleaner can see or take it; then it opens to everyone,
unchanged.

```
Order.PreferredEmployeeId    — what the customer ASKED FOR   (durable fact, already exists)
Order.PreferredHoldUntilUtc  — what the platform GRANTED     (policy outcome, new, nullable)

Order.GrantPreferredHold(employeeId, untilUtc)   — the ONLY writer; refuses a null beneficiary
Order.ClearPreferredHold()                       — drops both halves together
```

Two columns, two lifetimes, one **owner**. "We stored your preference but could not act on it" has to be
expressible; **"we granted a hold to nobody" must not be**, and the aggregate is what makes that true.

### The window (floor amended by the owner: 4 h → 8 h)

```
hold = 0                     when lead < 2 × BookingPolicy.StandardLeadTimeHours   (= 8)
     = min(lead × 0.10, 12h) otherwise
```

| Lead | Hold | Open to everyone | Notification |
|---|---|---|---|
| 2–8 h | **0** | 100% | **yes** — the notify half still fires (D4.1) |
| 8 h | 48 min | 90% | yes |
| 24 h | 2 h 24 | 90% | yes |
| 120 h + | 12 h (ceiling) | ≥90% | yes |
| 168 h (recurring) | 12 h | 93% | yes |

**`2 ×` a constant is not a second constant.** The express/hold relationship stays derivational: move
express to 6 h and the floor moves to 12 h, with no drift. That is what CH-7's "one number" property was
actually protecting, and it survives the owner's ruling intact.

**Invariant H (restated per SEAT — the draft's per-order version was false):**
> *At least 90% of every **seat's** fill window is always open to the entire board.* The hold covers the
> order's **first** seat only and is spent the instant any cleaner is assigned.

*Why:* `Order.cs:519` gives every order `MaxEmployees = RequiredEmployees + 1`, so the drafted predicate
locked the spare seat for the rest of the window **after** the perk had been delivered — to a beneficiary
`TakeOrder.cs:79-90` refuses a second seat. **A seat nobody on the platform could take.**

### The rule — five terms, two forms, six surfaces of four kinds

```
open ⟺ hold == null ∨ beneficiary == null ∨ hold <= now
                    ∨ beneficiary == caller ∨ AssignedEmployees.Any()
```

| Kind | Surfaces |
|---|---|
| queryable visibility | `OrderSpecification` (own `if` block) → `GetPagedOrders.cs:91`; `CreateAvailableOrdersSpec` → `GetAvailableJobsPreview.cs:50` **and** `GetDashboardStats.cs:236` |
| in-memory authorization | `OrderAccessService.CanBrowseOrderAsync:85` → `GetOrderDetails.cs:45` **and** `GetOrderPhotos.cs:58` |
| write gate | `TakeOrder.Validator`, **inside** the `ExistsAsync` rule |
| notification | `NewJobsDigestService` — as a conjunct, **plus** its own freshness rule |

Three traps the panel found and the ticket must avoid:

1. **`CreateAvailableOrdersSpec` never sets `RestrictToEmployeeId`** — the draft's wiring would have
   fixed one surface, left two leaking, **and passed the grep check**.
2. **`ExcludeEmployeeId` is the opposite polarity** — do not reuse it for the hold.
3. **`OrderSpecification.Create`'s parameters are all optional** — a caller that forgets the new
   argument **compiles green and leaks**. Verify **call sites**, never hit counts.

**Two evaluation forms, pinned by a test, not by a shared lambda.** SQL and C# disagree on null equality,
so a single shared `Expression` would not have made the two evaluators agree anyway. `TC-PREF-EQUIV-0`
runs the full fixture matrix against **PostgreSQL**. `.Compile()` on a request path is banned.

### The expiry has no actor — and neither does the consumption

`now >= PreferredHoldUntilUtc` is a `WHERE` clause; consumption is a row appearing in `OrderEmployees`.
**No job, no sweep, no outbox message, no status transition, no row change.** This is the single property
everything else hangs off, and it survived every attack — including a deliberate attempt to break it on
reschedule / cancel-recreate / return-to-board, which **failed** (there is no reschedule path at all).

### The digest — two rules, one surface (the subtle part)

`NewJobsDigestService` decides "new to this cleaner" by comparing the latest `OrderStatusTrack.CreatedOn`
against `Employee.LastNewJobsDigestAt`. **If a hold hides an order, at expiry its status track is older
than every cleaner's watermark and the order is never digested again** — board-only, forever.

The fix is **not** `max(latest track, hold) > watermark`. That compares against an instant in the
**future**, which marks the order "new" from creation, pushes cleaners about an order they cannot see,
inflates the push's count, and walks the watermark past the expiry — **the same defect, one layer up.**

```
-- conjunct 1: visibility (the SHARED rule, same as every other surface)
-- conjunct 2: freshness  (local to the digest, and the ONLY place this notion exists)
   EXISTS (h.CreatedOn > @since)
   OR (beneficiary <> @cleaner AND hold > @since AND hold <= @sweepStartedAt)
```

- **Disjunction, never `max`/`CASE`/`GREATEST`** — the value form compiles to a per-row `CASE` over two
  correlated aggregates plus a cast on a column.
- **The upper bound is the correctness condition**, not an optimisation.
- **The existing top-N is deleted, not wrapped** (`latest > k ⟺ ∃ > k`) — which makes this query
  **cheaper than it is today** and removes a latent index requirement.
- **`nowUtc` is `sweepStartedAt`** (`:57`), the value the sweep stamps. Never `UtcNow` in the loop.

> **Known structural limit, stated rather than papered over:** `LastNewJobsDigestAt` is a single
> per-cleaner scalar that assumes eligibility is monotone and derivable from a **global** timestamp.
> The overlap filter (`:135-143`) already breaks that assumption; the hold is the second such rule.
> **This is a point fix, not a class fix**, and the overlap variant is filed separately.

### Notification and the privacy line

- New event `order.preferred_offer`, produced inline in the create path, **bypassing the 30-minute digest
  cadence** and **not** stamping the watermark. Category: `NewJobsAvailable` (the existing mute governs).
- **The notification is granted on a WIDER predicate than the hold** (D4.1):
  `reachable-and-able ⇒ notify`; `notify + enough lead ⇒ hold`. *"No signal ⇒ no hold"* survives; its
  converse does not. This is what makes the 8-hour floor cheap (the 2–8 h band still gets the weaker
  half) **and** what lets one static customer sentence be true in both outcomes.
- **Reachability is three checks, not one:** the category mute, `Device.NotificationsEnabled`
  (`Device.cs:14-20` — a documented hard kill switch), and **no device row at all**. Consequence: until
  the partner web SPA registers devices, **the perk is effectively mobile-cleaner-only.**
- `Order.cs:221-222`'s *"not exposed to the cleaner side"* is **kept** for everyone not chosen and
  **deliberately dropped** for the one who was. Exclusivity is invisible to the excluded by construction.
- The take-time refusal is the existing **`OrderNotFound`**, folded **into the existence rule** so it
  cannot leak `NoAvailableSpots`. The catalog rule is the narrow one — *never introduce an error key that
  names the exclusivity* — because the strong "read and write must agree" form is **already violated** by
  shipped code (`TakeOrder.cs:44-45` vs `OrderSpecification.cs:134-139`).

### The customer

No countdown, no "waiting for Anna", no push on expiry, no customer-visible hold state in flight. But the
draft's *"told once, at the moment of choosing"* had **no surface**: both pickers render the explanatory
line as the `?:` fallback for the cleaner's **name**, so it is destroyed by the act it explains. So:

- a **persistent** second line on the picker row, both clients × 5 locales (**C2c**, budgeted at zero in
  the draft);
- the sentence must be **true in both outcomes** and must never name a decline reason — and must never
  say *"we'll still note your request"*;
- **no `firstChanceApplies` flag to the client in wave 1** — the answer is not stable between quote and
  submit, and a conditional promise that flips is worse than a static one that is true either way.

### The Plus gate

- Server-side, a **second `MustAsync`** in the existing `When(...)` block at `CreateOrder.cs:140-147`,
  using the one live-membership predicate.
- **Reject, do not silently ignore** — the same field already fails the whole order when the preference
  is ineligible (`CreateOrder.cs:143-146`). But **the error must name the tap**, not sell a subscription.
- **Existing non-member orders are left alone** and are inert by construction. **No backfill.**
- **A member who lapses keeps the hold on orders already created** (ADR-0009 D2 / ADR-0035 D1's freeze).
- **Recurring degrades instead of rejecting** — *reject where a human can react; degrade where nobody
  can.* A 03:00 sweep must never drop a customer's cleaning because a subscription lapsed.
- **`PastDue` is excluded from the predicate — and escalated** (`Q-PLUS-05`): the enum documents a grace
  window (`MembershipStatus.cs:18-19`) that no code implements, and this gate would make it load-bearing
  for rejecting a whole booking. Interim: one predicate, unchanged, **plus** the comment corrected.

---

## The copy debt (this is what ships first)

**ADR-0035's corrective-ships-first rule applies:** *"waiting for the mechanism to ship is choosing to
keep a false statement live for the length of a build."*

| Statement | Where | Class |
|---|---|---|
| *"he **will be assigned** first"* | web **cs/sk/ru `.json:1095`**, on the **checkout page** (`membership-subscribe.component.html:102-103`) | corrective — contradicts AC3 |
| *"prioritized when matching"* | web en/uk `.json:1095` | corrective — nothing reads the field |
| *"the same cleaner … on **every booking**"* | Android + iOS × 5 | corrective — false for every recurring occurrence |
| *"Cleaner being assigned · **Within 1 hour**"* | Android `values/strings.xml:741-742` + 4 locales; iOS same keys; **unconditional** | corrective — **already unbacked**; nothing on a pull board enforces or measures an hour |
| a Plus perk sold where there is no picker | web `en.json:1084`, `:1094-1095` | corrective |

Plus: the picker **title** is a *request* verb in five locales; the *"matching algorithm boosts that
cleaner's score"* myth lives in **three** files (`Order.cs:217-224`, `PreferredCleanerPicker.kt:52-54`,
`order-wizard.facade.ts:576-578`); and the web string is at **`en.json:1095`**, not `:1097`.

---

## Trade-off space (the map, kept current)

| Axis | Chosen | Live alternative | What would flip it |
|---|---|---|---|
| Mechanism | exclusive hold | board ordering / boost (A1) | evidence fill time is already marginal — first response is lowering the fraction, not switching |
| Window | proportional + ceiling | fixed duration (A3) | nothing surfaced by the panel |
| Hold floor | **`2 × StandardLeadTimeHours` (8 h)** — owner ruling | `1 ×` (4 h) | **settled; do not reopen** |
| Expiry | clock comparison + consumption | job / status transition (A5) | nothing — not close |
| Storage | stored deadline | duration at read time (A4) | nothing — A4 retroactively activates every legacy row |
| Hold scope | **first seat only** | whole order until expiry | nothing — the draft's version locked a seat nobody could take |
| Two eval forms | two members + equivalence test | one shared tree + `.Compile()` (A17) | a provider-level guarantee that SQL and C# null semantics agree — there isn't one |
| Digest freshness | bounded disjunction | `max(...)` (rejected) | nothing — the value form is both slower and wrong |
| Notify vs hold | **notify on a wider predicate** | notify only when held | evidence the extra push annoys cleaners it can't help |
| Non-member preference | reject | accept-and-ignore (A10) | a lead/owner ruling that revenue beats consistency |
| Eligibility rule | keep "completed order with them" | drop it (A12) | nothing — dropping it makes the perk a customer-controlled targeting primitive |
| Copy sequencing | **corrective first, affirmative with C2** | defer all copy to T-0491 | nothing — this is ADR-0035's ruling applied |

## Open / undecided

- **`Q-PLUS-05` (owner)** — does `PastDue` keep perks during Stripe's retries? Interim: no.
- **`Q-PLUS-04` (owner)** — should a lapsed member's recurring schedule keep materializing? The sweep
  checks membership nowhere today, so D8 revokes the *smaller* perk while the *larger* one survives.
- **The constants are uncalibrated**, and **`const` means a release** — not the free knob the draft
  claimed. Honest cost: one backend release, **no** client change. Measurement ticket is a precondition.
- **No `EXPLAIN`, no row counts.** The emitted SQL is known (a `ToQueryString()` harness); plan choice is
  reasoning. The sweep's per-cleaner loop (C queries + Σ N_c queries per run, 48×/day) is priced by
  reasoning only — redesign **filed, not preconditioned**.
- **Surface 2/6 use `{Pending, Confirmed}` while the digest uses `{New, Pending, Confirmed}`** under a
  comment claiming they mirror. Whether the board *should* show `New` is a product question — filed.
- **Admin visibility of a live hold** — not decided. (And no index exists to serve it: D5.5 rules out the
  partial index, so an admin hold view would need its own decision.)
- **A fallback list** (second-choice cleaner) — not considered.

## Consumers

| Ticket | Carries |
|---|---|
| ***new — C0, ships FIRST, depends on nothing*** | the corrective copy wave (the five false statements above) |
| **T-0515** | the hold: column (⚠️ `ef-migration`) + `Grant`/`ClearPreferredHold` + `OrderVisibility` (both forms) + `ComputePreferredHold` + the resolver + all six surfaces + **D5.3's rewritten digest clause** + the targeted push + the three comment corrections + `TC-PREF-EQUIV-0` |
| **T-0516** | the Plus gate (`Q-PLUS-03` **answered: plus-only**) + the `MembershipStatus` comment |
| **T-0491** | the copy — ten constraints and a **sequencing ruling** from ADR-0036 §Copy; **C2c ships with C2** |
| ***precondition of T-0515*** | `StampWatermarkAsync`'s tenant trap (`NewJobsDigestService.cs:211-220` loads tenant-scoped inside a tenant-ignoring sweep — the watermark can never advance under multi-tenancy, *after* the push is enqueued) |
| ***precondition of T-0515 starting*** | the measurement ticket (time-to-first-assignment by lead bucket; approved+active cleaners per `WorkCountryId`; share of orders never claimed) |
| *new, PM to file* | recurring carry-through (D8) — ⚠️ second `ef-migration`, ⚠️ `nswag-regen` |
| *new, PM to file* | the digest's overlap-filter variant of the watermark defect (pre-existing, same root cause) |
| *new, PM to file* | the digest sweep redesign (group by `WorkCountryId`; hoist the overlap loop; batch the preferences read) |
| *new, PM to file* | the web wizard has no preferred-cleaner picker at all |
| *new, PM to file* | should the available-orders board include `New` orders? |
