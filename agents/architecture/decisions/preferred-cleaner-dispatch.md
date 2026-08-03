# Preferred-cleaner dispatch — living decision notes

> **Status of this page: CURRENT SHAPE.** **[ADR-0036](../../backlog/adr/0036-preferred-cleaner-first-refusal-hold.md)**
> is **`accepted`** (2026-08-02, after a full defense panel: author + three challengers + lead). The ADR
> is the immutable record and carries the `## Challenge` / `## Defense` / `## Verdict` trail; **this page
> is the evolving companion and is what you read first.** `agents/knowledge/patterns-backend.md` now
> carries the enforceable rule (*"Bounded exclusivity on a pull board"*), and the role card is
> `agents/knowledge/roles/preferred-cleaner-hold-resolver.md`.
>
> ⚠️ **AMENDED 2026-08-03 by owner instruction.** **[ADR-0039](../../backlog/adr/0039-preferred-cleaner-slot-availability-is-checked-at-the-moment-of-choosing-set-based-and-never-earns-a-hold-when-it-fails.md)**
> (`proposed`) **partially supersedes ADR-0036 D5.1 / A6**: the preferred cleaner's availability at the
> booking's own date and time **is** now checked — at the picker and again at creation — and a cleaner
> known to be busy gets **no hold and no push**. **A6's weekly-cap half stands.** The hold mechanism
> itself is untouched. See §"Is this cleaner free at this hour?" below.
>
> ✅ **BOTH OWNER ESCALATIONS CLOSED 2026-08-03.** **`Q-PLUS-05`** → **`PastDue` keeps NO benefits; cut
> everything on the first payment failure, no grace window.** D7's interim ruling becomes binding and
> **not one line of D7 changes.** **`Q-PLUS-04`** → **a lapsed membership does NOT stop a recurring
> schedule**: occurrences keep being generated, at **full non-member price**, and the **customer is
> notified of the price change** (that notification **does not exist** — ticket **P-3**). D8.6's named
> asymmetry is now a **ruled** asymmetry. ADR-0036 carries a second dated amendment (AM-A / AM-B).
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

### Is this cleaner free at this hour? (ADR-0039 — owner instruction, 2026-08-03)

> *"there is a need to mark somehow if this cleaner has order assigned to him already or not on this
> date and time, if yes then mark that this cleaner isn't available for that date and time"*

**ADR-0036 D5.1 deliberately did not check this. It does now.** The original reasoning is preserved and
was not wrong — it priced the cost of a wrong creation-time answer in **latency**, bounded by Invariant
H. The owner prices a different cost: **a choice offered to a customer that we cannot honour.** The
case D5.1 never named is *busy at creation and still busy at the take* — up to 100% of the first seat's
fill window spent on an outcome with probability **zero**.

| | Ruling |
|---|---|
| **Where it is asked** | at the picker (the customer's chosen slot) **and** in the hold resolver at creation |
| **How** | **one set-based query** — `GetBusyEmployeeIdsInWindowAsync(ids, start, end)` — never `HasOverlappingOrderAsync` in a loop |
| **The agreement that matters** | picker and resolver call **the same method** with the same window. Not "the same rule" — the same call |
| **When busy** | **no hold AND no targeted push.** ADR-0036 D5.1's own words: *"a hold for a cleaner `TakeOrder.cs:53` would reject is pure latency — and so is a push."* New `HoldDeclineReason.CleanerBusyAtCleaningTime = 9`, sitting with `CleanerNotApproved`, **not** with `ShortLeadTime` |
| **The weekly cap** | **still not checked, and this is evidence not taste.** `GetEmployeeOrderCountThisWeekAsync:249-252` derives its window from `DateTime.UtcNow.Date` — at creation, for a booking 10 days out, it answers about a week that does not contain the booking |
| **Order in the resolver** | **last.** It is the only check that costs a range scan; every other gate is an equality on rows already fetched |

**Two verified defects the naive implementation walks into** — this is why it is an ADR and not a ticket:

1. **`HasOverlappingOrderAsync` is tenant-SCOPED (`GetDbSet()`, `OrderRepository.cs:281`) while its
   digest caller is tenant-IGNORING** (`NewJobsDigestService.cs:63,98,137`). Under a tenant every
   branch of the filter is false ⇒ **every cleaner reports free**. It is also the `TakeOrder` write
   gate. T-0529's status log asked the PM to file this; **it still has no ticket.**
   → *The defect is not "it is tenant-scoped" — it is that **one method serves two callers with
   opposite tenancy requirements and silently picks one**. Name the two variants (the shipped
   `EmployeeRepository.GetByIdAsync` / `GetByIdIgnoringTenantAsync` precedent).*
2. **No lower bound on `CleaningDateTime`** — the only range term is an **upper** one (`:283`), and
   `CleaningDateTime.AddMinutes(EstimatedTime)` (`:284`) is a per-row computation, not sargable. So each
   call scans the cleaner's whole assignment history. **×20 per picker render.**
   → *Floor the scan at `windowStart − BookingPolicy.MaxOrderSpanHours` (24 h) so
   `IX_Orders_CurrentStatus_CleaningDateTime` serves it. The number is chosen by **failure asymmetry**:
   too generous = a wider scan of a nearly-empty band; too tight = a missed overlap on the **write
   gate** = a double-booking. **When in doubt, widen it.** Checkable in one line:
   `SELECT MAX("EstimatedTime") FROM "Orders"`.*

**End state, not a parallel path:** `HasOverlappingOrderAsync` becomes a one-line wrapper over the set
method, so the floor and the tenancy fix land on the write gate for free and there is never a second
overlap predicate. `HasOverlappingOrderStatusTests` is the pin that proves it did not change meaning.

**The window's duration has exactly one definition.** A nominal window is wrong in both directions (too
short re-opens the failure; too long greys out a cleaner the customer could have had). `QuoteOrder`
does not return the estimate and the **client must not supply it** (S1). So `OrderFactory.cs:145-146`'s
inline sum is extracted to `OrderDuration.EstimateMinutes(services, packages)` with two callers and one
test (`TC-AVAIL-WINDOW-0`: the picker's window length equals `Order.EstimatedTime` for the same
selection).

**Where the answer is produced: `GetMyServingCleaners`, extended — never a general
`GET /employees/{id}/availability`.** The general endpoint is a schedule oracle for any employee id.
The extension keeps two limits **structural**: you can only ask about cleaners who have completed a job
for you, and only about the one instant you are booking. **No range parameter, ever** — that is a
different decision with a different privacy analysis.

The response field is a **tri-state**: `true` / `false` / **`null` = not evaluated**. `null` is
reachable on day one (a client that has not been rebuilt, no slot chosen yet, the check failing) and
**must render as no marking**. A client that maps it to a `Bool` either greys out everyone or defeats
the feature.

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

**And now (ADR-0039 D7) the unavailable marking, with its own constraints:**

- **shown, greyed, unselectable** — never hidden. Hiding manufactures a mystery to avoid writing a
  sentence, and a shorter list discloses the same fact anyway. The owner's word is *"mark"*.
- **one neutral line — *"Not available for this date and time"*.** It names **no reason** (not
  "booked", not "busy", not a time, not a count) and **promises no other time** (no "try 14:00", no
  "next available", no calendar affordance). Rationale that survives the next revision: it is a
  statement about **what Cleansia can offer**, not about what the person is doing — so it stays true if
  the predicate later widens to approval or work country. *"Already booked"* becomes a lie the moment
  it does.
- **the subtle constraint, and the easiest one to lose:** greying two of five rows implies the other
  three **are** available, which is a stronger claim than *first chance*. **C2c's persistent line is
  UNCHANGED.** The marking is subtractive only; nothing here may be written as *"these cleaners are
  available for your booking"*.
- **the race** (free at render, taken at submit): **the order is created, normally.** The preference is
  **stored**, the hold is **not** granted, no push, and **the customer is told nothing new** — D6/A8
  stand. This is deliberately *not* D7's membership rejection: ***reject where someone can react;
  degrade where nobody can*** (D8's rule). Membership is static and fixable in one tap; busyness is
  dynamic and the only "fix" is moving your own appointment.
- **what is disclosed, stated rather than argued away:** that a cleaner **who has been in this
  customer's home** is occupied during **the one window this customer chose**. Not who, where, what, or
  any other window. It is the minimum the feature cannot exist without — there is no way to say *"you
  cannot have Anna at 10:00"* without saying Anna is unavailable at 10:00. Residual probing is bounded
  by the serving-cleaner set and by the flag naming no reason. **`Q-AVAIL-04`** escalates whether
  cleaners should be told this is visible.

### The Plus gate

- Server-side, a **second `MustAsync`** in the existing `When(...)` block at `CreateOrder.cs:140-147`,
  using the one live-membership predicate.
- **Reject, do not silently ignore** — the same field already fails the whole order when the preference
  is ineligible (`CreateOrder.cs:143-146`). But **the error must name the tap**, not sell a subscription.
- **Existing non-member orders are left alone** and are inert by construction. **No backfill.**
- **A member who lapses keeps the hold on orders already created** (ADR-0009 D2 / ADR-0035 D1's freeze).
- **Recurring degrades instead of rejecting** — *reject where a human can react; degrade where nobody
  can.* A 03:00 sweep must never drop a customer's cleaning because a subscription lapsed.
- ✅ **`PastDue` is excluded from the predicate — SETTLED 2026-08-03 (`Q-PLUS-05`): no benefits, cut on
  the first payment failure, no grace window.** The interim ruling **is** the ruling; the predicate is
  unchanged and `MembershipStatus.cs:18-19`'s comment has been corrected. **Consequence that promotes a
  constraint:** the customer most likely to hit `PreferredEmployeeMembershipRequired` is now a **paying
  customer whose card expired and who has been told nothing** — `GetMyMembership` returns
  `HasMembership: false` for them, so the app shows the *subscribe* upsell. **T-0491's five translations
  must name the action, not sell a subscription.** That is now a requirement, not advice.

### Lapse × recurring — `Q-PLUS-04`, settled 2026-08-03

> **A lapsed (or `PastDue`) membership does NOT stop a recurring schedule.** Occurrences keep being
> generated, at **full non-member price**, and the customer is **notified of the price change**.

**Two thirds of this is already how the code behaves — verified by reading:**

| Leg | Expressible today? | Evidence | Cost |
|---|---|---|---|
| Keep generating | **yes — already true** | `MaterializeRecurringBookings.Handler`'s ctor (`:39-47`) takes **no** membership repository; the sweep selects on `IsActive`/`StartsOn`/`EndsOn` only (`:54-59`) | **zero** |
| Full non-member price | **yes — already true, and it composes with the `PastDue` ruling by construction** | the sweep calls `orderFactory.CreateAsync` per occurrence (`:141`); `OrderFactory.cs:76-83` re-reads the **one predicate per order** and applies the discount only when non-null ⇒ `PastDue` ⇒ 0 discount ⇒ full price, frozen | **zero** |
| **Notify of the price change** | **NO — does not exist** | the materializer takes no `INotificationProducer`; `recurring.scheduled` (`NotificationEventCatalog.cs:24`) is produced only by `SendRecurringOrderReminders.cs:77-87` with `orderId` + `orderNumber` — **no price** — and fires at ~T-24h while materialization runs 7 days ahead | **ticket P-3** |

**Why it composes with no special case:** the discount is resolved **per occurrence, inside the
factory**, from the shared predicate — not cached on the template, not frozen at template creation. So
`MaterializeRecurringBookings` **must not** acquire a membership repository for pricing reasons;
ADR-0036 D8.3 gives it one for the **preference** only and that scope is load-bearing.
⚠️ Related detail: `rawSubtotalResult` is hoisted **once per template** (`:105-113`) while the discount
is computed **per occurrence**. That split is correct — hoisting the discount too would freeze a
membership state across a whole batch.

**Two constraints on P-3 that are architecture, not copy:**

1. **One notification per PRICE TRANSITION, not per occurrence** — a weekly template would otherwise
   emit *"your price went up"* every week forever. Readable with **no new column**:
   `Order.MembershipDiscountAmount` (`Order.cs:207`) + `Order.RecurringTemplateId` make "the previous
   occurrence carried a discount and this one does not" one indexed query per template per sweep. A
   per-template stamp is the fallback; the invariant is one-per-transition either way.
2. **It must fire on the way back too** — a customer who fixes their card should be told the price went
   **down**. Omitting the good-news half turns a fairness mechanism into a dunning tool.

> ⚠️ **The composed consequence, stated plainly.** With D8.3 (recurring drops the preference on a failed
> gate) + the `PastDue` ruling, an expired card means the next **automatically generated** occurrence
> arrives having silently lost **four** things at once — the discount, the preferred cleaner, the hold,
> and (once ADR-0035 ships) the express waiver — on a booking the customer did not initiate, while
> `GetMyMembership` tells them they have no membership at all. **P-3 is the only thing between that and
> a chargeback**, which is why it is a precondition of running recurring in production, not a
> follow-up — and why **P-1** (a surface that says *"your card failed"*, filed off ADR-0035 AM-17) is
> its sibling.

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
| **Slot conflict at creation** *(ADR-0039)* | **checked — no hold, no push** | not checked, the hold expires (ADR-0036 D5.1/A6) | **settled by owner instruction 2026-08-03; do not reopen** |
| **Weekly cap at creation** | **still NOT checked** | check it too | nothing — `GetEmployeeOrderCountThisWeekAsync:249-252` keys on `UtcNow.Date`, so it answers about a week that may not contain the booking |
| **How the picker asks** *(ADR-0039)* | one **set-based** query over the customer's own serving set | N × `HasOverlappingOrderAsync`; a general `GET /employees/{id}/availability` | nothing — the loop is wrong under a tenant and unbounded; the general endpoint is a schedule oracle |
| **Overlap scan floor** *(ADR-0039)* | `windowStart − MaxOrderSpanHours` (24 h), chosen by failure asymmetry | a persisted end-instant column + index | `MAX(EstimatedTime)` approaching the floor, or the floor in a slow-query report |
| **Unavailable treatment** *(ADR-0039)* | shown · greyed · unselectable · one neutral line | hidden; greyed silently; selectable-with-a-warning; name the reason; offer another time | nothing — each alternative either lies, mystifies, or ships a claim we cannot back |
| **Race lost at submit** *(ADR-0039)* | create the order, store the preference, no hold, tell nobody | reject the booking; push the customer | support evidence that customers believe the preference was honoured → the answer is **copy**, not a push |
| **`PastDue` (card failed)** | **no benefits, immediately** — owner ruling 2026-08-03 | a grace window through Stripe's retries (what `MembershipStatus.cs` used to document) | **settled; do not reopen.** Support volume from customers who lost benefits before being told is the signal — and the answer is **P-1** (tell them), not a grace window |
| **Lapse × recurring schedule** | **keep generating, full price, notify** — owner ruling 2026-08-03 | stop materializing on lapse; keep the member price | **settled; do not reopen.** *Never drop a customer's cleaning* is the governing rule |

## Open / undecided

- ✅ ~~**`Q-PLUS-05` (owner)** — does `PastDue` keep perks during Stripe's retries?~~ **ANSWERED
  2026-08-03: NO.** Cut everything on the first payment failure; no grace window. Interim ruling became
  the ruling; **no `WHERE` clause changed**, which is the return on there being one predicate.
- ✅ ~~**`Q-PLUS-04` (owner)** — should a lapsed member's recurring schedule keep materializing?~~
  **ANSWERED 2026-08-03: YES** — at full non-member price, with the customer notified. D8.6's asymmetry
  (the *smaller* perk revoked on lapse, the *larger* one kept) is **confirmed as the ruled behaviour**.
  See §"Lapse × recurring" above. Notification = **ticket P-3**.
- **The constants are uncalibrated**, and **`const` means a release** — not the free knob the draft
  claimed. Honest cost: one backend release, **no** client change. Measurement ticket is a precondition.
- **No `EXPLAIN`, no row counts.** The emitted SQL is known (a `ToQueryString()` harness); plan choice is
  reasoning. The sweep's per-cleaner loop (C queries + Σ N_c queries per run, 48×/day) is priced by
  reasoning only — redesign **filed, not preconditioned**.
- **Surface 2/6 use `{Pending, Confirmed}` while the digest uses `{New, Pending, Confirmed}`** under a
  comment claiming they mirror. Whether the board *should* show `New` is a product question — filed.
- **Admin visibility of a live hold** — not decided. (And no index exists to serve it: D5.5 rules out the
  partial index, so an admin hold view would need its own decision.)
- **A fallback list** (second-choice cleaner) — not considered. *(ADR-0039 makes this question sharper,
  not answered: once the picker can say "not available", "then who?" is the customer's next thought.)*
- **`Q-AVAIL-04` (owner)** — should cleaners be told, in the partner app or the terms, that a past
  customer can see whether they are free for one specific slot? The disclosure is real, narrow and
  unavoidable if the feature exists (ADR-0039 D7.4). **Not blocking** — it changes text, not mechanism.
- **`BookingPolicy.MaxOrderSpanHours = 24` is a scan floor, not an enforced invariant.** Nothing caps
  `EstimatedTime` today. It is safe because it may only ever be *too generous*, and it is verifiable in
  one line — but if a booking ever exceeds it, an overlap disappears **on the write gate**. The durable
  fix is a persisted end-instant column (ADR-0039 A15), filed with its flip condition.
- **`GetMyServingCleaners` still lists cleaners `TakeOrder` would categorically refuse** (left the
  platform, wrong work country, not approved). ADR-0039 rules that this is a **filter on the list**, not
  a flag on the row — a different shape for a fact that never changes with the slot. Filed, not fixed.

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
| *new, PM to file* | the web wizard has no preferred-cleaner picker at all — **and it inherits ADR-0039's copy + tri-state constraints when it is built** |
| *new, PM to file* | should the available-orders board include `New` orders? — **answered by ADR-0037 D1** (`New` **+ Cash** yes, `New` + Card no) |
| **ADR-0039 — new, PM to file (A0)** | **`HasOverlappingOrderAsync` is tenant-scoped under a tenant-ignoring caller.** T-0529's status log asked for this and it was never filed. `security_touching`, ADR-0028's lane. **File first** — it is live on a shipped push path |
| **ADR-0039 — new, PM to file (A1)** | the set-based `GetBusyEmployeeIdsInWindowAsync` + both tenancy variants + `BookingPolicy.MaxOrderSpanHours` + `HasOverlappingOrderAsync` reduced to a wrapper. **Absorbs A0 if A0 has not shipped.** Must land with the floor from day one |
| **ADR-0039 — new, PM to file (A2)** | `OrderDuration.EstimateMinutes` extraction + `OrderFactory` rewire + `TC-AVAIL-WINDOW-0` |
| **ADR-0039 — new, PM to file (A3)** | `GetMyServingCleaners` gains the slot + the tri-state answer. ⚠️ **`nswag-regen`, owner-only**. Depends on A1 + A2 |
| **ADR-0039 — new, PM to file (A4)** | the picker UI marking + one string × 5 locales × 2 customer clients. Depends on A3 + the regen |
| **ADR-0039 → T-0515** | the resolver's busy check + `HoldDeclineReason.CleanerBusyAtCleaningTime`. **An added AC, not a new ticket** — T-0515 builds the resolver |
| **ADR-0039 → T-0491** | the unavailable string's constraints, **and** the constraint that C2c's line is unchanged (the marking must not upgrade the promise for the unmarked rows) |
| *new, PM to file* | `GetMyServingCleaners` materializes full order graphs for ≤20 names (pre-existing; optimizer lane) |
| *new, PM to file* | `GetMyServingCleaners` should drop cleaners `TakeOrder` would categorically refuse (a **filter**, not a flag) |
