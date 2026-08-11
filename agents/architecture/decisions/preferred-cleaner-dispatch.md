# Preferred-cleaner dispatch — living decision notes

> **Status of this page: CURRENT SHAPE.** **[ADR-0036](../../backlog/adr/0036-preferred-cleaner-first-refusal-hold.md)**
> is **`accepted`** (2026-08-02, after a full defense panel: author + three challengers + lead). The ADR
> is the immutable record and carries the `## Challenge` / `## Defense` / `## Verdict` trail; **this page
> is the evolving companion and is what you read first.** `agents/knowledge/patterns-backend.md` now
> carries the enforceable rule (*"Bounded exclusivity on a pull board"*), and the role card is
> `agents/knowledge/roles/preferred-cleaner-hold-resolver.md`.
>
> ⚠️ **AMENDED 2026-08-03 by owner instruction.** **[ADR-0039](../../backlog/adr/0039-preferred-cleaner-slot-availability-is-checked-at-the-moment-of-choosing-set-based-and-never-earns-a-hold-when-it-fails.md)**
> is **`accepted`** (2026-08-03, after a two-lane defense panel: disclosure + query-cost + lead;
> **sixteen findings, fourteen upheld, eleven amendments folded in**). It **partially supersedes
> ADR-0036 D5.1 / A6**: the preferred cleaner's availability at the booking's own date and time **is**
> now checked — at the picker and again at creation — and a cleaner known to be busy gets **no hold and
> no push**. **A6's weekly-cap half stands.** The hold mechanism itself is untouched. See §"Is this
> cleaner free at this hour?" below.
>
> 🔴 **The one thing to carry away from that panel: the feature has THREE server-side preconditions,
> and they gate the BACKEND ticket, not the UI one.** A rate limit, a server-side Plus gate on the
> flag, and an `IsActive` filter on the list. The oracle exists the moment the *server* answers —
> gating the picker UI would have shipped it anyway. See §"The three preconditions" below.
>
> ✅ **BOTH OWNER ESCALATIONS CLOSED 2026-08-03.** **`Q-PLUS-05`** → **`PastDue` keeps NO benefits; cut
> everything on the first payment failure, no grace window.** D7's interim ruling becomes binding and
> **not one line of D7 changes.** **`Q-PLUS-04`** → **a lapsed membership does NOT stop a recurring
> schedule**: occurrences keep being generated, at **full non-member price**, and the **customer is
> notified of the price change** (that notification **does not exist** — ticket **P-3**). D8.6's named
> asymmetry is now a **ruled** asymmetry. ADR-0036 carries a second dated amendment (AM-A / AM-B).
>
> 🔴 **CORRECTION, 2026-08-08 (lead, assign-and-confirm panel). This page said "Nothing is shipped yet."
> That is FALSE and it cost a round.** An ADR author read this page's premise plus a pre-fix source file
> and wrote a whole section designing work that already existed; a challenger caught it, and one of the
> draft's verification steps would have deleted correct customer copy. **Do not trust the dated snapshot
> below without re-reading HEAD.** Verified at HEAD by the lead, by opening each file:
>
> | Claim on this page | Truth at HEAD |
> |---|---|
> | *"Nothing is shipped yet"* (was line 30) | **False.** ADR-0036's mechanism ships. |
> | *"**Consumption** — **None.** No query, no ordering, no notification, no assignment reads `PreferredEmployeeId`"* | **False on all four.** The pair is `Order.cs:246` / `:264`, written only by `GrantPreferredHold` (`:424-435`) and dropped only by `ClearPreferredHold` (`:438-443`, sole production caller `:689`). Both forms of the visibility rule are `OrderVisibility.cs:36-52`, and `TakeOrder`'s existence gate conjoins it at `TakeOrder.cs:88`. The targeted push is produced at `OrderFactory.cs:192-205` under `NotificationEventCatalog.PreferredOffer` (`:57`), is in the partner feed keyset (`NotificationFeedEventKeys.cs:51`) and has live five-locale partner copy (`partner-app/src/main/res/values/strings.xml:1244-1245`). |
> | *"**No membership check** — a non-member can set it today"* | **False for the hold path**: `PreferredCleanerHoldResolver.cs:42-47` returns `Declined(NoMembership)` with no active membership, ahead of seven further gates (`:32-100`). *(The `CreateOrder`-side gate was not re-verified in this pass — re-read before citing it.)* |
> | *"`RecurringBookingTemplate` has no field to pass"* | **False.** `MaterializeRecurringBookingTemplate.cs:240` carries `template.PreferredEmployeeId` into every occurrence. |
> | *(not on this page at all)* | The window is `BookingPolicy.ComputePreferredHold` (`:171-180`) — `min(lead × 0.10, 12 h)`, zero below `2 × StandardLeadTimeHours` — with the fraction at `:159` and the ceiling at `:160`. |
>
> **Also shipped 2026-08-08, in an independent lane:** the customer-side `order.cleaner_assigned`
> notification (`NotificationEventCatalog.cs:37`, one shared producer `OrderCleanerAssignedNotifier.cs`,
> both assignment call sites, both customer clients, five locales), and `order.confirmed`'s copy
> corrected to *"Booking confirmed ✅"* (`customer-app/…/values/strings.xml:1211`). **A new ADR must not
> propose minting that key.**
>
> **This page is rewritten in full when the assign-and-confirm ADR is accepted** (it is `proposed`, under
> `backlog/adr/drafts/`, revised after a challenge round — see its §Verdict). The correction above is a
> statement of fact and applies no unaccepted decision.
>
> Companion pages: [`membership-benefits.md`](./membership-benefits.md) (ADR-0035 — the express waiver
> this composes with), [`push-notifications.md`](./push-notifications.md) (ADR-0025 — the display
> contract the targeted push rides), [`outbox.md`](./outbox.md) (ADR-0002/0008).
> Business view: `agents/analysts/notifications.md`. Published view: `docs/architecture/backend.md`.

---

## Today (a DATED SNAPSHOT — verified 2026-08-02, **superseded 2026-08-08**; read the correction banner above first)

> ⚠️ **This table describes the code as it stood on 2026-08-02, before ADR-0036 and ADR-0039 landed.**
> Its "Consumption — None", "No membership check" and "Copy" rows are false at HEAD. It is kept because
> the *capture* and *picker* rows are still the honest starting point of the story, not because the
> table as a whole is current. **Cite HEAD, never this table.**

**On 2026-08-02: the customer could express a preference and the platform did nothing with it.**

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

**Two verified defects the naive implementation walked into — ✅ BOTH NOW FIXED AND SHIPPED** (they
landed while the panel was sitting; kept here because the *shape* is the reusable part):

1. ✅ **`HasOverlappingOrderAsync` was tenant-SCOPED while its digest caller is tenant-IGNORING**
   (`NewJobsDigestService.cs:63,98,137`). Under a tenant every branch of the filter is false ⇒ **every
   cleaner reports free** — on the digest *and* on the `TakeOrder` write gate.
   → *The defect is not "it is tenant-scoped" — it is that **one method serves two callers with
   opposite tenancy requirements and silently picks one**.* **Fixed as `HasOverlappingOrderAsync` /
   `HasOverlappingOrderIgnoringTenantAsync` (`OrderRepository.cs:272-276`)**, both delegating to one
   private predicate parameterised by the queryable. Pinned by
   `HasOverlappingOrderTenancyAndScanFloorTests` — **every tenancy case seeds a non-null `TenantId`**,
   which is the only way these tests prove anything (`security-rules.md:236`).
2. ✅ **No lower bound on `CleaningDateTime`** — the only range term was an **upper** one, and
   `CleaningDateTime.AddMinutes(EstimatedTime)` is a per-row computation, not sargable, so each call
   scanned the cleaner's whole assignment history. **×20 per picker render.**
   → **Fixed as a scan floor at `OrderRepository.cs:289`.** ⚠️ **Two corrections the panel forced on
   the ADR's draft, both of which the shipped code already got right:**
   - **The constant is `Order.MaxOrderSpanHours` in `Cleansia.Core.Domain`, NOT `BookingPolicy`.**
     `Cleansia.Infra.Database` does not reference `Cleansia.Core.AppServices` — the drafted placement
     **does not compile**.
   - **It is 168 h, not 24.** *"24 h exceeds any single-day span by construction"* was false against
     shipped seed data: the catalog produces **20.25 h from services alone and 58.25 h with packages**.
     168 gives ~3× headroom over a number an admin can raise tomorrow.

**End state — ⚠️ the convergence direction is INVERTED from the draft, and the shipped shape is
better.** The draft made the boolean a wrapper over the **set** method (`.Count > 0` over a
`.Distinct().ToListAsync()`), which would have made `TakeOrder`'s hot path materialize a set to answer
a boolean. The shipped code shares the **predicate**, not the terminal operation, and keeps
`.AnyAsync(ct)`. So:

```
LiveCommitmentsInWindow(IQueryable<Order>, startUtc, endUtc)   ← ONE definition (floor + interval + status)
   ├─ .Any()        one employee, tenant-scoped     → TakeOrder            ✅ shipped
   ├─ .Any()        one employee, ignoring          → NewJobsDigestService ✅ shipped
   └─ .SelectMany() N employees, tenant-scoped      → picker + resolver    ⬅ the only piece left to build
```

**No `IgnoringTenant` sibling of the set method will be built** — it would have zero callers on the day
it shipped (the digest is boolean-shaped and already has one), and a tenant-escaping read one
IntelliSense entry from a customer-facing handler is a cost with no buyer. **Two pin classes** hold the
predicate through the extraction: `HasOverlappingOrderStatusTests` (11 status cases) and
`HasOverlappingOrderTenancyAndScanFloorTests` (5 tenancy + 3 floor cases, including the accepted blind
spot written down as a passing `Assert.False`).

**A second fan-out shape exists and is deliberately NOT built here.** The digest and the recurring
materializer are **one employee, many windows** — the set method does nothing for either, and both
loops survive. That is pre-existing, on timers, and belongs to the filed digest redesign; ADR-0039 D3.3
specifies its shape (one query per cleaner over `[min(starts) − floor, max(ends))`, intersected in
memory against a **pure interval function in Domain**, status staying in SQL) so the redesign cannot
invent a fourth predicate.

> ⚠️ **The composition trap with ADR-0037, because it fails OPEN.** Offerability is
> `STATUS(o) ∧ NotRetractable(o)`. **Do not add `NotRetractable` to the occupancy predicate.** It is an
> *entry* condition — nothing can be assigned unless it was offerable — so it is redundant on the happy
> path and actively wrong where it is not: an order whose payment state changed *after* a cleaner took
> it would stop blocking, double-booking a cleaner who is standing in someone's flat. Retraction is
> expressed the one correct way, by a sweep moving the order to `Cancelled`, which is terminal and
> already absent from `SlotBlockingStatuses`. **Occupancy = a commitment already made. Offerability =
> work still available. One predicate each.**

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
reachable on day one and **must render as no marking**. A client that maps it to a `Bool` either greys
out everyone or defeats the feature.

> ⚠️ **The tri-state dies two layers below the UI, and both clients have the idiom that kills it.**
> `OrderApi.kt:343-352` (`toAppDto`) drops the whole row on any absent field — three `?: return null`
> lines running — and `ServingCleanersClient.swift:19-24` does the same via `compactMap`/`guard let`
> into a **non-optional** struct. Combine that with the two "hide the picker when the list is empty"
> guards (`PreferredCleanerPicker.kt:94`, `PreferredCleanerViewModel.swift:23-25`) and a transient query
> failure **deletes the picker** — no error, no log, no support signal. That is not "degrades to
> today's behaviour"; it is *the feature was never there*.
>
> **The contract, binding on the mobile ticket:** the tri-state survives into the **app-layer DTO** as
> `Boolean?` / `Bool?` — it is not resolved at the mapping boundary — and the pin is an **automated test
> per client** (payload with the field absent ⇒ non-null row with `isAvailable == null`), not a
> reviewer's eyeball.
>
> **The generalisable rule (catalog gap, filed):** *an optional field added to an existing response row
> must never be mapped with the row-dropping idiom.* The idiom is right when absence makes the row
> **meaningless** (an id) and wrong when absence **is an answer** (this field).
> `generated-client-contract.md` covers the NSwag/TypeScript side of added optionals and has nothing on
> the hand-written mobile mappers.

**`null` now has THREE producers, and that is the seam holding rather than defensive programming:**

| producer | why |
|---|---|
| no slot in the request / the check could not run | the original two — degrade, never a lie |
| **a non-member** | the Plus gate moved server-side and sits on the **flag**, not the list (see the preconditions below) |
| **a cleaner-side suppression flag** *(reserved, not built)* | if `Q-AVAIL-04`'s lawful-basis answer allows workers to object. `false` would **leak the opt-out itself**; `true` asserts what the server declined to evaluate; **`null` already means "not evaluated"** |

### The three preconditions (ADR-0039 D12 — they gate the BACKEND ticket)

The draft's safety argument rested on two structural limits. The panel found **one real and one
imaginary**, and the imaginary one was carrying the whole privacy case.

| Limit | Status |
|---|---|
| *You can only ask about cleaners who completed a job for you* | ✅ **real and structural.** `Cancelled` is terminal and not `Completed`; reaching `Completed` needs a cleaner to take and photo-gate-finish the job; and a customer cannot target a specific cleaner because `PreferredEmployeeId` is itself gated on already-completed membership — **circular by construction.** The set accumulates by luck, one paid cleaning at a time. |
| *You can only ask about one instant* | ❌ **enforced by nothing.** |

**Why the second one was fiction, three ways:**

1. **No rate limit at all.** `MyServingCleaners` carries no `[EnableRateLimiting]` on either customer
   host, while `CancelOrder` **eleven lines above it in the same controller** (`:171` vs `:182`) carries
   `[EnableRateLimiting("auth")]` — and `RateLimitPolicies.cs:152-156`'s `GlobalLimiter` returns
   `GetNoLimiter("authed-global")` for anyone with a `sub`. **84 unthrottled requests = an
   hour-resolution weekly calendar for 20 named cleaners.** And the grid is a *client* convention:
   `CleaningDateTimeUtc` is a free `DateTime`, so the real resolution is bounded by the rate limit and
   nothing else.
2. **The window end is caller-chosen.** `SelectedServiceIds` + `SelectedPackageIds` pick it, and
   nothing capped it — **58.25 h reachable from the shipped catalog.** A range parameter under another
   name, which the ADR's own compliance grep **passed green on**.
3. **The Plus gate was client-side only.** Mobile view models guard the fetch; the handler injects no
   membership repository. So the oracle served every customer with ≥1 completed order.

**The three fixes, and they gate the server ticket (A3), never the UI ticket (A4)** — `curl` does not
need a client:

| # | Fix | Shape |
|---|---|---|
| **1** | `[EnableRateLimiting("auth")]` on `MyServingCleaners`, **both** customer hosts | **The existing policy — 30/min per `sub`** (`RateLimitPolicies.cs:47-49`), same as the sibling `CancelOrder`. **No new policy**: `Cleansia.Config` registers them **once for all five hosts**, so a fourth policy is a shared-config change + an ADR-0003 amendment to buy a number we already have. Sharing the `auth` budget with login/cancel is deliberate — probing should cost the prober everything else. **⇒ the mobile ticket owes a debounce**, or the first 429 lands on a customer scrubbing a time control. |
| **2** | The **Plus gate on the flag, not the list** | Non-member ⇒ `null` on every row, list unchanged. Gating the *list* would change a shipped contract and empty it for non-members, which both clients render as *the picker never existed*. |
| **3** | An **`IsActive` filter** on the picker's employee **and** its user | S10. Today a departed / GDPR-erased cleaner is returned (soft-delete: `GdprDeletionService.cs:235-241` → `IsActive = false`, the `Completed` order survives) — and, having no live orders, they compute **free**, render unmarked and selectable, and earn the hold. |

**Precondition 3 has a twin that is NOT ADR-0039's and does not wait for it:**
`HoldDeclineReason.CleanerNotApproved` keys on `ContractStatus`, and `Deactivated(...)` leaves
`ContractStatus` **untouched**. So under **ADR-0036 alone, today**, a hold + a targeted push can be
granted to a cleaner who left the platform — 100% of the first seat's fill window on a zero-probability
outcome, and with `SpareSeatsPerOrder = 0` it is the *only* seat. **The resolver's approval gate must
read `IsActive`. Added as an AC on T-0515.**

### The span cap — the assumption the floor rests on is now enforced

`Order.MaxOrderSpanHours = 168` documents itself as **"assumed, not enforced"**, and nothing caps
`EstimatedTime`: no ceiling on a service's estimate (`GreaterThanOrEqualTo(0)`), no cap on the item
count (`CreateOrder`'s only cardinality rule is a **lower** bound of one), and a service inside a
selected package is summed **again** if also selected directly.

**Ruling: `BookingPolicy.MaxBookableOrderSpanHours = 24`, enforced at `OrderFactory` — the single
production writer of `EstimatedTime` — before `CalculateRequiredEmployees` runs**, mirrored in the
`CreateOrder` / `QuoteOrder` validators, and pinned by a unit test asserting
`MaxBookableOrderSpanHours <= Order.MaxOrderSpanHours`.

**Two numbers, deliberately, in two layers — conflating them is what produced the compile error:**

| | `Order.MaxOrderSpanHours = 168` | `BookingPolicy.MaxBookableOrderSpanHours = 24` |
|---|---|---|
| Kind | **query safety margin** | **product policy** |
| Layer | `Core.Domain` (the repository can reach it) | `Core.AppServices` (where the write path lives) |
| Must | dominate every *producible* span | reject what a real appointment never is |
| If wrong | a missed overlap **on the write gate** = a double booking | a customer's large booking is refused |

**It is also, unavoidably, a crew cap** — `RequiredEmployees = ceil(EstimatedTime / 120)` and
`SpareSeatsPerOrder = 0`, so 24 h implies a maximum crew of 12. Flagged for the owner, not decided.

**Why it blocks, and the lane matters:** on the **double-booking** lane it does *not* block (168 h with
3× headroom is real but not urgent). On the **disclosure** lane it **does** — 58.25 h of
caller-controlled window is a binary-search primitive the day the flag ships. A reader who thinks the
cap is about double-booking will correctly de-prioritise it and ship the oracle anyway.

### What is measured rather than asserted

**"No new index" is an expectation, not a fact.** The status term sits inside an `OR` with the
fail-closed NULL fallback, so it is **not** an unconditional index qual, and the only unconditional
sargable conjunct is a range on `IX_Orders_CurrentStatus_CleaningDateTime`'s **second** key column —
which a PG16 btree cannot start a scan on. The claim depends on the planner choosing a **`BitmapOr`** of
two index paths. It probably will (the NULL arm is empty on any database built from the single
`Initial` migration), but *probably* is not what the ADR said.

- **Obligation:** `EXPLAIN (ANALYZE, BUFFERS)` in `src/Cleansia.IntegrationTests` — the rig exists
  (`PostgresContainerFixture` + real migrations), which is why ADR-0036's "priced by reasoning" caveat
  is **not** inheritable here.
- **Flip condition:** a seq scan or full index scan ⇒ emit the two arms as a `Concat`/`UNION` so both
  are sargable by construction. **Not** a new index — that pays maintenance on the hottest insert path.
- **The cheapest fix is escalated, not adopted:** make `Orders.CurrentStatus` **`NOT NULL`**. The
  column is nullable only for pre-backfill rows and the repo carries **exactly one migration**, so that
  population is empty by construction. It deletes the disjunct, the correlated subquery and the risk
  **for every consumer of the column**. Out of the panel's authority (it supersedes an `accepted`
  ADR-0037 ruling and needs an owner-only migration) and **time-boxed** — near-free while the owner is
  regenerating `Initial`, a backfill afterwards. **`Q-AVAIL-05`, needs its own ADR.**
  > **✅ ADOPTED, 2026-08-04 — it got its ADR:
  > [ADR-0040](../../backlog/adr/0040-order-currentstatus-is-non-nullable-the-pre-backfill-population-it-defends-does-not-exist.md)**
  > (`proposed`). **This does not close the `EXPLAIN` obligation two bullets up** — ADR-0040 claims the
  > status term becomes an unconditional qual on the leading index column (a *shape* claim), not a
  > measured plan. The flip condition above (`Concat`/`UNION`) becomes moot only if ADR-0040 is
  > accepted; keep it until then.

### The 127-column elephant, re-sequenced

`GetMyServingCleaners` materializes the **full order graph** for every completed order of the customer
to produce ≤20 names: **127 columns pulled, 4 used** — including `IBAN`, `PassportId`,
`RegistrationNumber` and `VatNumber` from `Employees` — with **no `AsNoTracking()`**, and `GroupBy` /
`Take(20)` running **in memory**.

**The projection ships WITH or BEFORE the flag, not after.** Not because of the cycles: because pulling
`IBAN` and `PassportId` into a **customer-facing** handler is an over-fetch with a security shape, and
because the flag ticket is editing this exact query anyway (for the `IsActive` filter and the
membership gate). Pushing `Take(20)` into SQL also bounds the busy query's input **at the source**
rather than after materializing everything.

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
  cannot have Anna at 10:00"* without saying Anna is unavailable at 10:00.
- **the residual is a NUMBER, not an adjective** (the panel struck the word *"determined"*): reachable
  **set** ≤20 own serving cleaners (structural); reachable **depth** ≤24 h per request (enforced);
  reachable **rate** 30/min per `sub`, shared with that account's login and cancel. A full week of
  hour-resolution answers therefore costs ~3 minutes of sustained requests and the account's entire
  auth budget while it runs. **No mechanism here makes bulk reconstruction impossible** — a rate limit
  buys slowness, budget-consumption and countability. The owner is accepting that arithmetic.
- ⚠️ **and after the picker becomes slot-keyed, probing is the ORDINARY interaction.** Both clients
  fetch once today (`PreferredCleanerPicker.kt:85-92` behind a one-shot latch,
  `PreferredCleanerViewModel.swift:27-29`), and the "clearing" rule invalidates that premise. Post-fix,
  every scrub of the date control emits an occupancy answer, from customers attacking nothing. That is
  accepted — and it is why the **debounce** is not a polish item.
- **`Q-AVAIL-04`** — ⚠️ **re-scoped by the panel from *notice* to *lawful basis*.** Not "should
  cleaners be told" but *which basis covers disclosing a worker's occupancy to a third party for that
  third party's convenience*, balanced against the **quantified** residual above rather than the
  one-slot story. The platform has a real posture to answer within (`ConsentType` + `UserConsent` with
  grant/withdraw, `IpAddress`, `UserAgent`; cleaners are `User`s). **Still non-blocking, and now for a
  reason:** the likely outcome (workers may object) needs a suppression flag, and **`null` is already
  reserved for it** — so the answer changes text unless it changes nothing.

### The customer's offer block — PRESENCE, not just content (ADR-0049, 2026-08-11)

ADR-0045 §D7.2 settled what the block **contains**. It said nothing about **when it is sent**, and that
gap is a shipped defect: `PreferredOffer.StateOf` derives from four columns with no fulfilment status
and no seat count (`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:36-53`), and nothing downstream
withholds the result — the web facade's `visible()` is `state !== None || canChooseAnother()`
(`order-preferred-offer.facade.ts:61-63`).

So the card says *"The request for the cleaner you asked for has ended. This booking is now open to our
whole team."* (`apps/cleansia.app/src/assets/i18n/en.json:1740-1741`) in three situations where it is
false: a cancelled booking, a finished one, **and a live booking a different cleaner already took**.

**The finding that decided the layer.** T-0595 proposed a shared order-status grouping, on the model of
iOS's `OrderStatusGroup.isUpcoming`. **No status grouping can express the third case.** Every candidate
membership contains `Confirmed`, so on a `Confirmed`, fully-staffed booking iOS's own fix
(`OrderStatusMapping.swift:37-40` → `PreferredOfferPresentation.swift:23-24`) still produces the false
sentence. The distinguishing term is a **seat count** — `Order.AvailableSpots`
(`src/Cleansia.Core.Domain/Orders/Order.cs:136`) — which is not a client's arithmetic to do.

**The shape.** A second pure function beside `StateOf`:

```
IsDisclosable(state, currentStatus, availableSpots)
  =  currentStatus ∉ {Completed, Cancelled}                        // (a) the booking concluded
  ∧  ¬(state == Closed ∧ availableSpots <= 0)                      // (b) the search is over
```

and `GetOrderDetails.ResolvePreferredOfferAsync` returns `null` when it is false — reusing the
nested-optional channel the DTO already has, so **no wire change and no `nswag-regen`**. The safety
property, asserted rather than assumed: **`¬IsDisclosable ⇒ ¬PreferredOfferExit.IsOpen`**, provable
from `PreferredOfferExit.cs:40-49` + `OrderAvailability.cs:60-63` + the `MaxEmployees ≥ 1` invariant
(`Order.cs:112`, `:697-707`, `:709-718`), so withholding can never hide a live re-choose affordance.

**Limb (b) uses free seats, not assignments — and the difference is most of the order book.** The
tempting term is `AssignedEmployees.Count > 0`, which is what `IsOpen` itself uses
(`PreferredOfferExit.cs:46`). It is wrong for a *sentence about the booking*:
`RequiredEmployees = ceil(EstimatedTime / 120)`, so a booking over two hours carries a second seat and
*"open to our whole team"* stays true while that seat is free. **Withhold false sentences, not stale
ones.**

**What each stack owes** (detail in ADR-0049 §D6): backend ships the whole fix; **web adds a pin, not a
narrowing** — a status conjunct in the facade would collide with the mutation guard `d5ba1484` left at
`order-preferred-offer.models.spec.ts:164-188`; **iOS keeps** its `isUpcoming` conjunct as knowing
duplication with a written retirement condition; **Android needs nothing**, and its customer app does
not map the block at all.

**Two things ruled deliberately, so they are not re-derived.** (1) **No platform-wide order-status
grouping.** (2) **`upcoming` is not a status-predicate name** — it already means a clock rule on web
(`orders.component.ts:124-126`) and a status rule on iOS, and a web lane reached for the wrong one.

> #### 🔴 CORRECTION 2026-08-11 (lead, ADR-0049 amendment C1) — the argument for (1) changed inside one sprint
> This page used to justify (1) as *"the three sets all differ, each for a stated reason"*, with the
> first row reading *"a `static readonly` array **because EF inlines it into SQL**"*. **Both halves are
> now wrong and neither may be reused.**
>
> - **The memberships no longer differ.** `746a5064` restored the missing `OnTheWay` to
>   `GdprDeletionService.ErasureBlockingStatuses` (`GdprDeletionService.cs:104-111`), which was this
>   ADR's own §Found-en-route report. It is now **identical** to `OrderRepository.SlotBlockingStatuses`
>   (`OrderRepository.cs:264-271`) and the two are **pinned equal by**
>   `src/Cleansia.Tests/Features/Gdpr/ErasureBlockingOrderStatusTests.cs:98-122`. The third,
>   `AdminOverrideOrderStatus.cs:86-97`, was never a live-order set — it is a *target*-status refusal
>   that keeps two error keys apart.
> - **The "EF inlines it into SQL" argument does not support the conclusion.** It explains why the field
>   is `static readonly`; it says nothing about whether *two* sites may share *one* array. A flat status
>   set is **data**: the same array translates through `.Contains` in an EF `Where`
>   (`OrderRepository.cs:344`) and runs in memory unchanged (`GdprDeletionService.cs:119`).
>   `OrderAvailability` needs two forms because it is a compound **expression**; that precedent does not
>   transfer to a set.
>
> **The ruling survives on the ground that replaced it:** *two questions with one answer today are not
> one question.* "Does a live commitment occupy this cleaner's slot", "does a live order refuse this
> subject's erasure" and "is this reservation sentence still worth saying" can diverge, and a shared
> constant makes that divergence **silent** because the second caller inherits it. **Two named sets
> pinned equal make agreement a decision re-made on every change** — which is the shape the tree landed
> on, and it is better than either merging or leaving them unpinned.
>
> **Extract on a condition, never a count.** `IsDisclosable`'s limb (a) is already the **third**
> expression of this membership and is pinned to neither of the others. Extract when a site needs the
> membership and cannot state its own reason inline, or when a divergence is proposed that no pin would
> catch. *Residual, named:* a new `OrderStatus` member reddens the GDPR pin but **not** limb (a) nor
> `PreferredOfferDisclosureTests`' `[InlineData]` table — so it would arrive silently disclosable.

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
| **Overlap scan floor** *(ADR-0039, amended by panel)* | **`windowStart − Order.MaxOrderSpanHours` (168 h), in `Core.Domain`** | ~~`BookingPolicy.MaxOrderSpanHours` (24 h)~~ — **does not compile** (Infra.Database cannot reference AppServices) and **refuted by seed data** (catalog reaches 58.25 h); a persisted end-instant column + index | `MAX(EstimatedTime)` approaching the floor, or the floor in a slow-query report |
| **Producible span** *(ADR-0039 D3.4 — new)* | **enforced: `BookingPolicy.MaxBookableOrderSpanHours = 24`, rejected at `OrderFactory`** | assumed, documented, unenforced (what shipped) | nothing — an unenforced safety-asymmetric constant is a comment. Raising the cap requires raising the floor first (`cap <= floor` is a unit test) |
| **Convergence direction** *(ADR-0039 D3.2, inverted by panel)* | **share the PREDICATE; the boolean keeps `.AnyAsync()`** | the boolean becomes a wrapper over the **set** method (`.Count > 0`) | nothing — the draft's direction cost `TakeOrder`'s hot path a materialized set + hash aggregate to answer a bool |
| **Plus gate on the picker read** *(ADR-0039 D12.1 — new)* | **server-side, on the FLAG** (non-member ⇒ `null` per row) | client-side only (today); or gate the whole **list** | nothing — client-side is not a gate; gating the list changes a shipped contract and both clients render an empty list as *no picker* |
| **The picker's rate limit** *(ADR-0039 D12.1 — new)* | **`[EnableRateLimiting("auth")]`** — the existing 30/min per-`sub` policy | none (today); a new narrow named policy | evidence that honest slot-scrubbing hits 30/min **after** the debounce — then the answer is a fourth policy + an ADR-0003 amendment, not a bigger `auth` |
| **Departed cleaners in the picker** *(ADR-0039 D12.3 — new)* | **filtered out** (`IsActive` on employee **and** user) | left in and marked; left in unmarked (today) | nothing — before the flag a stale row was cosmetic; after it, the absence of a mark **is a claim**, and it is false |
| **Unavailable treatment** *(ADR-0039)* | shown · greyed · unselectable · one neutral line | hidden; greyed silently; selectable-with-a-warning; name the reason; offer another time | nothing — each alternative either lies, mystifies, or ships a claim we cannot back |
| **Race lost at submit** *(ADR-0039)* | create the order, store the preference, no hold, tell nobody | reject the booking; push the customer | support evidence that customers believe the preference was honoured → the answer is **copy**, not a push |
| **`PastDue` (card failed)** | **no benefits, immediately** — owner ruling 2026-08-03 | a grace window through Stripe's retries (what `MembershipStatus.cs` used to document) | **settled; do not reopen.** Support volume from customers who lost benefits before being told is the signal — and the answer is **P-1** (tell them), not a grace window |
| **Lapse × recurring schedule** | **keep generating, full price, notify** — owner ruling 2026-08-03 | stop materializing on lapse; keep the member price | **settled; do not reopen.** *Never drop a customer's cleaning* is the governing rule |
| **Who decides the offer block is still worth saying** *(ADR-0049 — new)* | **the server withholds the whole block** (`IsDisclosable`) | a shared client-side `OrderStatus` grouping on three stacks (T-0595's proposal); a fifth `StateOf` input; a fifth enum member; a `shouldRender` flag beside the block | a *second* server consumer of `StateOf` — then the two functions merge behind one entry point returning `PreferredOfferState?` |
| **The "search is over" term** *(ADR-0049 — new)* | **`AvailableSpots <= 0`** | `AssignedEmployees.Count > 0` (what `IsOpen` uses) | the `Closed` copy splitting its two jobs — *"your request ended"* survives a filled booking, *"open to our whole team"* does not |
| **iOS's redundant `isUpcoming` conjunct** *(ADR-0049 — new)* | **kept**, with a written retirement condition | deleted in the same wave | ⚠️ **the backend change being DEPLOYED — not merged, not "the file being opened for any other reason"** (amendment C4). Then it goes and its two tests repoint at the absent-block case. A shipped iOS binary cannot be redeployed, so deleting early reopens the defect for an App Store review window plus the update tail. **Two carriers required first:** the doc comment beside `PreferredOfferPresentation.swift:24` (which today argues the **opposite**, `:16-19`) and a `blocked-by: backend DEPLOYED` row on the ticket |

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
- **`Q-AVAIL-04` (owner)** — ⚠️ **re-scoped: lawful basis, not notice** (see §The customer above).
  **Not blocking**, and now demonstrably so — `null` is reserved for a suppression flag today, so an
  opt-out outcome changes text rather than mechanism.
- 🕐 **`Q-AVAIL-05` — ROUTED, 2026-08-04: it has its ADR.**
  **[ADR-0040](../../backlog/adr/0040-order-currentstatus-is-non-nullable-the-pre-backfill-population-it-defends-does-not-exist.md)**
  (`proposed`) rules `Orders.CurrentStatus` **`NOT NULL`**, partially superseding ADR-0037 §D3's
  NULL ruling (which becomes *vacuous* rather than wrong). The write-time guarantee is verified there,
  not assumed: one production creation path (`OrderFactory.cs:104` → `:179` `AddOrderStatus(New)` →
  `:180` `Add`), one column writer, nothing clears it, no SQL path inserts an `Orders` row. **The
  owner decision that remains is only the one already scheduled** — the `Initial` regeneration; no
  separate authorization is needed, and no new `questions/open.md` entry was filed. **Two challenger
  lanes are open** (`write-guarantee`, `query-plan`); implementation runs in parallel because the
  window closes at the regeneration. **What this does NOT discharge:** the `EXPLAIN (ANALYZE,
  BUFFERS)` obligation above. ADR-0040 claims a *shape* change (the status term becomes an
  unconditional qual on the leading index column), **not** a measured plan improvement.
- ✅ ~~**`BookingPolicy.MaxOrderSpanHours = 24` is a scan floor, not an enforced invariant.**~~
  **Both halves resolved.** The constant shipped as **`Order.MaxOrderSpanHours = 168` in
  `Core.Domain`** (the drafted placement did not compile; 24 was refuted by a catalog reaching 58.25 h),
  and ADR-0039 D3.4 adds the **enforced** `BookingPolicy.MaxBookableOrderSpanHours = 24` at
  `OrderFactory`, so the floor's premise is finally true by construction. A15 (a persisted end-instant
  column) stays recorded as the durable answer with its flip condition unchanged.
- **`GetMyServingCleaners` still lists cleaners `TakeOrder` would categorically refuse** (wrong work
  country, not approved). ADR-0039 rules that this is a **filter on the list**, not a flag on the row —
  a different shape for a fact that never changes with the slot. ⚠️ **The `IsActive` half is no longer
  "filed, not fixed": it is a PRECONDITION of the flag** (D12.3), because after the flag ships the
  absence of a mark is an affirmative claim.
- **The digest's nested N+1 survives** (`NewJobsDigestService.cs:86` × `:135` → `:137`) — every
  approved/active cleaner platform-wide × their unpaged candidate orders, one round trip each. The set
  method does nothing for it: that loop is **one employee, many windows** and the set method is **many
  employees, one window**. Pre-existing, on a timer, owned by the filed digest redesign — whose shape
  ADR-0039 D3.3 now specifies so it cannot invent a fourth overlap predicate.
- **Two catalog rules the panel found missing** (filed, not written here): (1) **S5 does not cover
  read oracles** — it is scoped to *"auth + side-effecting endpoints"*, which is exactly why an
  unthrottled per-subject read shipped; the rule wanted is *a read whose answer is per-subject and whose
  repetition reconstructs a dataset is rate-limited like a side-effecting one*. (2) **`patterns-mobile`
  has no rule on hand-written response mappers** — `generated-client-contract.md` covers the
  NSwag/TypeScript side of added optionals only, and both customer clients carry the row-dropping idiom
  that silently kills an added optional.

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
| ~~**ADR-0039 (A0)**~~ | ✅ **DONE — shipped before the panel ruled.** `HasOverlappingOrderIgnoringTenantAsync` + the digest switch + non-null-`TenantId` pins. **Do not file.** |
| **ADR-0039 — new, PM to file (P1)** ⛔ | **precondition of A3** — `[EnableRateLimiting("auth")]` on `MyServingCleaners` (**both** customer hosts) + the **server-side Plus gate on the flag**. `security_touching` |
| **ADR-0039 — new, PM to file (P2)** ⛔ | **precondition of A3** — `BookingPolicy.MaxBookableOrderSpanHours` + the `OrderFactory` guard (before `CalculateRequiredEmployees`) + both validators + the error key × 5 locales + the `cap <= floor` test |
| **ADR-0039 — new, PM to file (P3)** ⛔ | **precondition of A3** — the `IsActive` filter on the picker's employee **and** its user. Fold into A6's edit; it is the same `.Select()` |
| **ADR-0039 — new, PM to file (P4)** | the `*IgnoringTenant*` architecture test (no reference under `Core.AppServices/Features/**`, with an allow-list). ADR-0028's lane, generalises past this feature. **Retires a manual review step.** Not blocking |
| **T-0595** *(ADR-0049)* | `PreferredOffer.IsDisclosable` + the `null` return in `ResolvePreferredOfferAsync` + `PreferredOfferDisclosureTests` (including the `¬IsDisclosable ⇒ ¬IsOpen` row) + **two web test rows and no web production code**. No migration, no `nswag-regen`, no i18n |
| ***new, PM to file** (ADR-0049 §D8)* | rename iOS's `OrderStatusGroup.isUpcoming` — it reads *"not concluded"* and collides with web's date-based `isUpcoming`. One file, no behaviour change. **Not ridden into T-0595** |
| ***new, PM to file** (ADR-0049 §D6)* | Android customer has **no** favourite-cleaner disclosure at all — its hand-written `OrderDtos.kt` does not map `preferredOffer`. Same gap T-0580 closed for web; it inherits a correct server and needs no grouping |
| ~~***new, PM to file** (ADR-0049 §Found en route)*~~ | ✅ **DONE — `746a5064`. Do not re-file.** `OnTheWay` restored to `GdprDeletionService.ErasureBlockingStatuses` (`:104-111`), both directions pinned plus set-equality with `OrderRepository.SlotBlockingStatuses` by `ErasureBlockingOrderStatusTests`. **That fix is what falsified ADR-0049 §D7's evidence** — see the CORRECTION block above and amendment C1 |
| ***new, PM to file** (ADR-0049 amendment C4)* ⛔ | **the two carriers for the iOS `isUpcoming` retirement**: a doc comment beside `PreferredOfferPresentation.swift:24` stating the narrowing is now the server's and the conjunct is deleted **only once the backend change is live on the target environment**, plus a `blocked-by: backend DEPLOYED` row on the deletion ticket. **Until both exist, deleting the conjunct is a review finding** |
| ***new, PM to file** (ADR-0049 amendment C1(v))* | exhaustiveness over `Enum.GetValues<OrderStatus>()` in `PreferredOfferDisclosureTests`, so a new status member cannot arrive silently disclosable. **Gate it on the probe first:** add a temporary member and see whether the suite already reddens |
| **ADR-0039 — new, PM to file (A1)** — **S**, was M | extract `LiveCommitmentsInWindow` from the shipped private predicate + add `GetBusyEmployeeIdsInWindowAsync` as a third wrapper. **No ignoring sibling.** ⚠️ **+ AC: `EXPLAIN (ANALYZE, BUFFERS)` in `Cleansia.IntegrationTests`; + AC: overlap cases re-run against real PostgreSQL** (the shipped pins run on SQLite). Both pin classes stay green |
| **ADR-0039 — new, PM to file (A2)** | `OrderDuration.EstimateMinutes` extraction + `OrderFactory` rewire + `TC-AVAIL-WINDOW-0` + **the picker's SQL `Sum` projection held to the same test** (the draft cited `ExistWithIdsAsync` as precedent — that is a `CountAsync` that materializes nothing) |
| **ADR-0039 — new, PM to file (A3)** | `GetMyServingCleaners` gains the slot + the tri-state answer. ⚠️ **`nswag-regen`, owner-only**. Depends on A1 + A2 **and on P1 + P2 + P3** |
| **ADR-0039 — new, PM to file (A4)** — **M**, was S | the picker UI marking + one string × 5 locales × 2 clients — **plus** slot-keyed re-fetch (latch removal, in-flight cancellation, **debounce**), selection-clearing state, and **`OrderApi.kt:343-352` + `ServingCleanersClient.swift:19-24` carrying `Boolean?`/`Bool?` into the app DTO with an automated pin per client**. Depends on A3 + the regen |
| **ADR-0039 → T-0515 (extra AC)** | **the resolver's approval gate must read `IsActive`, not `ContractStatus` alone.** `Deactivated(...)` leaves `ContractStatus` untouched, so ADR-0036 **as it stands today** can grant a hold + push to a cleaner who left. Does not wait on ADR-0039 |
| **ADR-0039 — catalog, PM to file ×2** | (1) **S5 amendment** — a per-subject read whose repetition reconstructs a dataset is an oracle and is rate-limited like a side-effecting endpoint. (2) **`patterns-mobile`** — an optional field added to an existing response row must never be mapped with the row-dropping idiom |
| **ADR-0039 → T-0515** | the resolver's busy check + `HoldDeclineReason.CleanerBusyAtCleaningTime`. **An added AC, not a new ticket** — T-0515 builds the resolver |
| **ADR-0039 → T-0491** | the unavailable string's constraints, **and** the constraint that C2c's line is unchanged (the marking must not upgrade the promise for the unmarked rows) |
| **ADR-0039 — new, PM to file (A6)** — **re-sequenced** | `GetMyServingCleaners` materializes full order graphs for ≤20 names: **127 columns pulled, 4 used**, tracked, including `IBAN` + `PassportId` into a **customer-facing** handler. `.Select()` + `AsNoTracking()` + `OrderByDescending`/`Take(20)` into SQL. ⚠️ **Ships WITH or BEFORE A3, not after** — the reason is the over-fetch's security shape and the fact that A3 edits this exact query anyway |
| *new, PM to file* | `GetMyServingCleaners` should drop cleaners `TakeOrder` would categorically refuse (a **filter**, not a flag). ⚠️ **The `IsActive` half is split out as P3 and BLOCKS A3**; the work-country / not-approved half stays here |
