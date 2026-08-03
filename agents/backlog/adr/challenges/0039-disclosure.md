# ADR-0039 — Challenger (privacy / disclosure lane)

Role: CHALLENGER. Gate 0 discipline: every claim below is traced to `file:line` I read, with the
exploit trigger named and the existing guards checked. Where a boundary held, it is recorded in the
final section instead of being inflated into a finding.

**Headline:** the ADR's central privacy claim — *"you can only ask about one instant"* (D5 limit 2,
D7.4's hard line, verify #11) — is **not enforced by the design**. It is enforced by nothing. Two
independent reasons: there is **no rate limit on the endpoint at all**, and the ADR's own request
shape contains a **duration parameter under a different name**. The disclosure the ADR priced is
"one cleaner, one window". The disclosure it actually ships is a queryable calendar.

---

### CH-D1 — `GetMyServingCleaners` carries **no rate limit whatsoever**, so "one instant per interaction" is a UI convention, not a bound; 84 requests reconstructs a week of 20 named cleaners' working hours

The ADR routes the flag through `GetMyServingCleaners` precisely so the question is structurally
narrow, and D7.4 concedes only that *"a determined customer can probe by re-opening the picker across
slots"*. That concession assumes probing costs something. It costs nothing.

Evidence:

- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Web.Customer/Controllers/OrderController.cs:182-190`
  and `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Web.Mobile.Customer/Controllers/OrderController.cs:182-190`
  — the endpoint carries `[HttpGet("MyServingCleaners")]` + `[Permission(Policy.CanViewPagedUserOrder)]`
  and **no `[EnableRateLimiting]`**. Contrast `CancelOrder` twelve lines above it (`:170-171`), which
  has `[EnableRateLimiting("auth")]`.
- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Config/RateLimiting/RateLimitPolicies.cs:152-164`
  — the `GlobalLimiter` returns `RateLimitPartition.GetNoLimiter("authed-global")` for any request with
  a `sub`. Authenticated callers are deliberately exempt from the global cap. So an endpoint with no
  named policy is, for an authenticated caller, **completely unthrottled**.
- The probe space is tiny and public: `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs:35,40-41`
  — `WindowDurationMinutes = 60`, `FirstWindowHour = 8`, `LastWindowHour = 20` ⇒ **12 bookable slots
  per day**. The set is `Take(20)` (`GetMyServingCleaners.cs:50`).

**Exploit trace (concrete, reachable the day A3 ships).** An ordinary customer with a valid bearer
token runs, in a loop:
`GET /api/Order/MyServingCleaners?cleaningDateTimeUtc=<slot>&selectedServiceIds=<any one id>`
for the 12 slots × 7 days = **84 requests**. No 429 at any point. Response is a JSON array of
`{employeeId, fullName, isAvailableForRequestedSlot}` for up to 20 cleaners. That output *is* a
one-hour-resolution weekly work calendar for twenty named people. 30 days = 360 requests. The line
the ADR says it "draws and does not cross" — *no calendar view, ever* — is crossed by the client the
ADR ships, in under a minute, with no tooling beyond `curl`.

**Amplifier the ADR does not price: this is not a Plus-only surface.** The Plus gate is *client-side
only* — `PreferredCleanerPicker.kt:70,85-92` (`isPlus` guards the fetch) and
`PreferredCleanerViewModel.swift:34` (`guard membership.hasMembership else { return }`). The handler
injects no membership repository (`GetMyServingCleaners.cs:19-21`) and the policy resolves to
`PhysicalPolicy.Authenticated` (`PolicyBuilder.cs:18`). So the oracle is available to **every**
customer with ≥1 completed order, member or not — an audience the ADR's "the perk" framing never
counts.

**Verdict: S5 FAIL for the new shape.** Not "a category of concern": the specific request above, at
the specific missing attribute, on the specific endpoint. The ADR asserts a structural limit it does
not build. Minimum fix: `[EnableRateLimiting("interactive")]` is **not** sufficient either (60/min ⇒
a week in 90 seconds) — this needs its own narrow per-`sub` window sized to real picker use (a booking
flow opens the picker a handful of times), and the AC must state the number so a reviewer can check it.

---

### CH-D2 — `SelectedServiceIds` + `SelectedPackageIds` **is** a range parameter. D7.4's hard line and verify #11 both pass on a request that spans 20+ hours

D5's ruling is `Query(CleaningDateTimeUtc, SelectedServiceIds, SelectedPackageIds)`. D4 then derives
the window end as `start + OrderDuration.EstimateMinutes(services, packages)`. Client picks the start;
client picks the ids; **client therefore picks the end.** `(start, end)` chosen by the caller is a
range query. The ADR rejected alternative A3 (`?from=&to=`) as "a schedule oracle" and then shipped
the same two degrees of freedom spelled differently.

Evidence the range is wide and uncapped:

- Duration is a plain sum with no ceiling — `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs:145-146`:
  `selectedServices.Sum(s => s.Service!.EstimatedTime) + selectedPackages.Sum(p => p.Package!.IncludedServices.Sum(...))`.
- Nothing caps the *count* of ids. `QuoteOrder.Validator` (`QuoteOrder.cs:74-80`) checks only that the
  ids **exist** (`ExistWithIdsAsync`) — the exact validator D4 tells the picker's handler to mirror
  (`:61-65`). `CreateOrder`'s only cardinality rule is `OrderMustNotBeEmpty` (`CreateOrder.cs:156-157`)
  — a **lower** bound of one.
- I summed the shipped catalog. `sql-scripts/insert_seed_data.sql` (Services INSERT block beginning at
  the column list on `:565-568`) contains 10 service rows with `EstimatedTime`
  `[120,180,45,90,60,90,75,240,180,135]` = **1215 minutes = 20.25 hours**. Selecting all ten in one
  picker request asks *"is any of my twenty cleaners busy at any point in the next 20¼ hours?"*.
  Adding packages (whose `IncludedServices` sum on top, and which re-count services already selected)
  pushes it past 24 h.

**Why this matters beyond aesthetics.** Wide + narrow probes together are a binary search: ~7 requests
localise a busy block inside a week instead of 84. But the more damaging point is procedural — the
ADR's own compliance step **cannot detect this**. Verify #11 says *"No range parameter exists anywhere
on the picker query… `rg -n "availability"` returns no endpoint."* That grep passes green against a
query that spans 20 hours, because the range is spelled `SelectedServiceIds`. A verification step that
passes on a violating implementation is worse than no step: it launders the violation.

**Collateral, and it damages D3.1's safety argument directly.** D3.1 justifies `MaxOrderSpanHours = 24`
with *"`EstimatedTime` is the sum of the booked services' estimates for a single appointment on a
single day; 24 h exceeds any single-day span **by construction**."* That is false against the shipped
catalog: an order that legitimately selects all ten services carries `EstimatedTime = 1215` — **84% of
the floor**, from seed data, today, with no adversary. And verify #6's check
(`SELECT MAX("EstimatedTime") FROM "Orders"`) samples *created orders only* — a probe never creates an
order, so the one empirical check the ADR designed is structurally blind to the input the caller
controls. "By construction" is a hope; there is no constructor enforcing it.

**Ask:** the picker query must not accept a service/package selection at all. Either (a) the handler
derives the duration from a server-side cart/quote the customer has actually assembled, or (b) the
window is the **booking grid slot** (`BookingPolicy.WindowDurationMinutes = 60`) and the resolver —
which runs at submit against a real, validated selection — remains the only place the true duration is
used. Option (b) makes the picker's answer slightly conservative in the ADR's *safe* direction
(over-greying), costs nothing, and restores "one instant" as a fact rather than a slogan. If the panel
keeps the selection fields, D7.4's hard line must be rewritten to say what is actually true, and
verify #11 must be replaced with an assertion on `windowEnd - windowStart`.

---

### CH-D3 — The tri-state dies in two mapper functions the ADR never names, and the failure mode is **fail-closed to an empty picker with no error**

The panel asked what a client does with `null` today. Answer: both clients have an **established
convention of collapsing optionals**, and following it kills the feature silently.

- Android: `/Users/michael/Desktop/Mike/Projects/cleansia/src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/core/orders/OrderApi.kt:343-352`
  ```kotlin
  private fun GenGetMyServingCleanersResponse.toAppDto(): ServingCleanerDto? {
      val employeeId = employeeId ?: return null
      val fullName = fullName ?: return null
      val lastServedOn = lastServedOn?.toString() ?: return null
      ...
  ```
  Three consecutive lines establishing "absent field ⇒ **drop the whole row**". The app DTO it targets
  has only non-null fields (`OrderDtos.kt:354-359`). An implementer adding
  `isAvailableForRequestedSlot` in this file's own style writes
  `val isAvailable = isAvailableForRequestedSlot ?: return null`.
- iOS: `/Users/michael/Desktop/Mike/Projects/cleansia/src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/Confirm/ServingCleanersClient.swift:19-24`
  — same two idioms, `guard let id = item.employeeId … else { return nil }` (drop) and
  `fullName: item.fullName ?? ""` (silently substitute). `ServingCleaner` (`:5-8`) is a non-optional
  struct.

**The consequence, traced.** `null` is reachable on day one by the ADR's own D8 table (old client, no
slot yet, check failed). Combine `?: return null` with the two visibility guards:
`PreferredCleanerPicker.kt:94` — `if (!isPlus || cleaners.isEmpty()) return`; and
`PreferredCleanerViewModel.swift:23-25` — `isVisible = isPlus && !cleaners.isEmpty`. Result: **on any
transient query failure the entire picker vanishes from the booking sheet, with no error, no log, no
support signal** — a Plus benefit that intermittently does not exist. D8 promises this case
"degrade[s] to today's behaviour, which is a degradation, not a lie." The code path available to the
implementer degrades to *the feature was never there*, which is worse than either.

The ADR's guard against this is verify #7 ("flip one row to `null` in a fixture and watch the UI") — a
**manual, human** step, pointed at the UI, defending against a mistake that happens **two layers below
the UI in a mapper the reviewer is not told to open**. And §D10 A4 scopes the mobile work to
`PreferredCleanerPicker.kt:167-176` and `:131-135` only — neither `OrderApi.kt:343` nor
`ServingCleanersClient.swift:19` appears anywhere in the ADR.

**Ask:** A4's file list must name both mappers; the tri-state must survive as `Boolean?` / `Bool?`
**into the app-layer DTO** (`ServingCleanerDto`, `ServingCleaner`), not be resolved at the mapping
boundary; and the pin must be an automated test per client (`toAppDto` on a payload with the field
absent returns a **non-null** row with `isAvailable == null`) rather than a reviewer's eyeball.

---

### CH-D4 — Both clients fetch the list **once**, so the ADR's slot-keyed premise and D7.1's "Clearing" rule require a fetch-lifecycle rewrite — and that rewrite is what makes probing the *default* interaction rather than a determined attack

- Android `PreferredCleanerPicker.kt:85-92`: `LaunchedEffect(isPlus) { if (isPlus && !loaded) { … loaded = true } }`
  — keyed on `isPlus`, guarded by a one-shot flag. The file's own doc comment says so:
  *"Eligibility list is fetched once per opening of the booking sheet — no cache invalidation, since
  the set only grows on order completion"* (`:56-58`). That premise is exactly what ADR-0039 invalidates:
  the answer now depends on the chosen slot, not only on the set.
- iOS `PreferredCleanerViewModel.swift:27-29`: `func load() async { if loaded { return }; loaded = true … }`
  — fetched once per view-model lifetime.

Two consequences:

1. **A4 is not an "S".** D7.1 rules that changing the slot must **clear** a now-unavailable selection.
   That requires re-fetching on every slot change on both clients — a lifecycle change to the
   `LaunchedEffect` key and to the `loaded` latch, plus selection-clearing state, plus in-flight
   request cancellation for a value a customer scrubs through. The ADR budgets "the picker UI + the
   string".
2. **It changes the disclosure's character, which is my lane.** D7.4's residual reads *"a determined
   customer can probe by re-opening the picker across slots"* — the word "determined" carries the whole
   argument. After A4 makes the fetch slot-keyed, **probing is the ordinary interaction**: one probe
   per scroll of the date/time control, performed by a user who is not attacking anything. The system
   will emit a stream of occupancy answers to every booking customer as a side effect of normal use,
   and (per CH-D1) will not count them. D7.4 must be rewritten against the post-A4 client, not the
   pre-A4 one.

---

### CH-D5 — The set-membership gate is the ADR's whole privacy argument, and for the highest-harm threat it selects **precisely the wrong population**

D5 limit 1 and D7.4 both rest on: *the only people who can see this are cleaners who have personally
completed a job in this customer's home.* I verified that gate is real and not cheap to widen (see the
sound-boundary section — it genuinely holds, and I want that on the record). My challenge is not that
the gate leaks; it is that **the gate is being used as a safety argument when it is not one.**

The gate reads: *the requester has had this worker inside their home.* For a bulk-scraping or
commercial-espionage threat model that is a genuine narrowing. For the threat model that actually
attaches to lone domestic workers — a customer who has fixated on a cleaner who came to their flat —
membership in the set is not a filter, it is **the risk factor itself**. The population the gate admits
is exactly the population with prior physical proximity and a known face and full name. The ADR grants
that specific person a per-hour occupancy oracle over the whole future board (CH-D1), and — critically
— *a busy answer is a location-and-time answer in disguise*: it tells the requester when the worker is
**not** at home and is **at some customer's flat in the service area**.

D7.4's "why it is accepted" argues from *unavoidability*: "there is no way to tell a customer *you
cannot have Anna at 10:00* without telling them Anna is unavailable at 10:00." That is true and I do
not dispute it **for the one slot the customer is actually booking**. It is not an argument for the
84-slot version, and the ADR conflates them because it assumes the one-instant limit is enforced. Fix
CH-D1 and CH-D2 and this challenge reduces to a documentation point; leave them and D7.4's acceptance
rationale is defending a feature that was not built.

**Ask:** D7.4's "The residual, named" paragraph must state the *quantified* residual after the rate
limit is chosen — e.g. "at N requests/minute a customer can resolve at most M slots per day" — so the
owner is accepting a number rather than an adjective.

---

### CH-D6 — S10: the picker already lists **deactivated and GDPR-erased** cleaners, and ADR-0039 converts that pre-existing list defect into an affirmative *"available"* claim

This is the one place where the ADR makes an existing defect materially worse rather than inheriting it.

- `GetMyServingCleaners.cs:26-33` filters only `o.UserId == userId && o.CurrentStatus == Completed`.
  There is **no `IsActive` filter** on the employee or the user, and none is inherited:
  `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/BaseRepository.cs:148-158`
  — `GetQueryable() => GetDbSet() => Context.Set<TEntity>()`, tenant filter only. EF `Include`s are
  never filtered.
- A departing / erased cleaner is soft-deleted, not removed:
  `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Services/GdprDeletionService.cs:235-241`
  calls `user.Employee.Deactivated(...)` and `user.Deactivated(...)`, and
  `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Common/Auditable.cs:35-42`
  sets `IsActive = false`. The historical `Completed` order survives (it is anonymised on the
  *customer* side only).

**The chain ADR-0039 adds.** A deactivated cleaner has no live orders ⇒ they are **not** in the busy
subset ⇒ `IsAvailableForRequestedSlot = true` ⇒ D7.1 renders them as an ordinary, **selectable**,
unmarked row. The customer selects them. `CreateOrder`'s eligibility validator still passes —
`CreateOrder.cs:150-154` → `UserHasCompletedOrderWithEmployeeAsync` (`OrderRepository.cs:294-305`)
asks only whether a `Completed` order exists, never whether the employee is still active. So the
preference is stored and (per ADR-0036) a hold + targeted push are produced for someone who left the
platform — 100% of the first seat's fill window on a zero-probability outcome, which is the precise
failure D2 exists to eliminate.

D7.3 anticipates the *shape* of this ("marking three rows must not silently promise the other two")
and answers it with **copy** — keeping ADR-0036's C2c line. Copy does not fix a false positive. And
D11 defers the real fix (A17, "a filter on the list") to a separate ticket with **no dependency
ordering against A3/A4**. Before this ADR the picker made no availability claim, so a stale row was a
cosmetic wart. After it, the absence of a mark *is* a claim, and it is wrong.

**Ask: A17's list-filter (at minimum `IsActive` on the employee and its user) must BLOCK A4, not
follow it.** Shipping the flag onto an unfiltered list is shipping a lie the ADR itself forbids
("never offer a cleaner the customer cannot actually have").

---

### CH-D7 — The tenancy split is correct as specified and **not** reachable from a customer endpoint today (LATENT), but the ADR's guard against regression is a naming convention plus verify step #2's eyeball

I was asked specifically whether the ignoring variant is reachable from a customer-facing endpoint.
**It is not, and I could not construct a trace.** Recording that plainly rather than inflating it:

- I enumerated every `GetQueryableIgnoringTenant()` call site in `Cleansia.Core.AppServices`:
  `PayPeriodBackgroundService.cs:117`, `NewJobsDigestService.cs:63,98,156`,
  `PeriodReminderBackgroundService.cs:53,79`, `PruneOutbox.cs:71`,
  `AutoCancelStaleRecurringOrders.cs:63`, `SendMembershipLifecycleNotifications.cs:76,113`,
  `SendRecurringOrderReminders.cs:63`, `DataRetentionBackgroundService.cs:77,83,103,125,146,183,211,252`.
  **Every one is a timer/sweep/retention job. Not one is on a request path.** The convention holds
  today.
- Even a mis-wired picker would be a *narrow* cross-tenant read, not an id leak: the `employeeIds`
  argument is derived from the caller's own tenant-scoped completed orders
  (`GetMyServingCleaners.cs:26-28`), so the ignoring variant could only widen the *evidence* used to
  mark a cleaner the caller can already see — it would disclose "this person has activity somewhere
  you cannot see", not who or where. Real, but small, and **latent**: ADR-0028 has not activated
  multi-tenancy, so with `TenantId = null` everywhere the two variants are byte-identical today and no
  test that seeds `tenantId: null` can tell them apart (the trap `security-rules.md:236` names).

The challenge is the **enforcement**, not the ruling. D6 fixes "a *method* picks tenancy for its
callers" by adding a second public name — and then relies on call sites choosing correctly forever.
But `GetQueryableIgnoringTenant()` is already `public virtual` on `BaseRepository.cs:153` and therefore
on every repository interface that any handler injects, including the `IOrderRepository` that
`GetMyServingCleaners.Handler` takes (`GetMyServingCleaners.cs:20`). Adding
`GetBusyEmployeeIdsInWindowIgnoringTenantAsync` to that same interface puts a tenant-escaping read one
IntelliSense entry away from the customer-facing handler that most wants a "why is this returning
nothing?" answer — and the failure is silent by construction (`security-rules.md:220-237`: *"the sweep
does not fail; it silently agrees with you"*).

**Ask (cheap, mechanical, and it generalises past this ADR):** an architecture test asserting that no
type under `Cleansia.Core.AppServices/Features/**` references any member whose name contains
`IgnoringTenant`, with an explicit allow-list. That converts D6 from a rule a reviewer must remember
into a rule the build enforces, and retires verify step #2's manual walk. Also — per
`security-rules.md:236` — the pinning tests for **both** variants must seed a **non-null `TenantId`**,
or they prove nothing.

---

### CH-D8 — `BookingPolicy` lives in AppServices, not Domain: D3's floor constant is **unreachable from the repository as written**, and the cheap workaround is a hard-coded safety bound

Not primarily my lane, but the constant is safety-asymmetric in a direction with a security
consequence, so I am raising it rather than assuming the architecture lane will.

- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs:1`
  — `namespace Cleansia.Core.AppServices.Features.Orders;`
- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj:21-23`
  — references `Cleansia.Core.Domain` and `Cleansia.Core.Queue.Abstractions`. **No reference to
  `Cleansia.Core.AppServices`.**

D3's implementation sample puts `BookingPolicy.MaxOrderSpanHours` inside `OrderRepository`
(Infra.Database). That does not compile. The ADR's *Applies to* line asserts `Cleansia.Core.Domain`
carries "one `BookingPolicy` constant" — the ADR appears to believe `BookingPolicy` is already in
Domain. It is not, and moving it is not free (10 files reference it, including `Order.cs`,
`OrderFactory.cs`, `CancellationPolicyResolver.cs`, `MembershipPlan.cs`, `OrderPricingCalculator.cs`).

Why this reaches my desk: D3.1 states the floor "may only ever be TOO GENEROUS" because a too-tight
floor makes an overlapping order invisible — **and D3.2 converges `TakeOrder`'s write gate onto the
same predicate**, so a drifted floor double-books a cleaner. The path of least resistance for an
implementer who hits the compile error is a literal `-24` in `OrderRepository`, i.e. a safety bound
with no home, no doc comment, and nothing tying it to the `EstimatedTime` it must dominate. The ADR
must say where the constant lands (Domain, moved, as its own ticket sequenced before A1) rather than
leave it to be discovered at build time.

---

### CH-D9 — `Q-AVAIL-04` is scoped as "tell them or not"; the real question is **lawful basis**, and the answer can change the mechanism — so the ADR should reserve the shape now, not later

D7.4 escalates *"whether cleaners should be told"* and rules it *"not blocking… it changes text, not
the mechanism."* The first half is right to escalate. The second half is the part I dispute.

What the platform already has, which the ADR does not reference:
- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Enums/ConsentType.cs:6-12`
  — `TermsOfService`, `PrivacyPolicy`, `MarketingEmails`, `DataProcessing`.
- `/Users/michael/Desktop/Mike/Projects/cleansia/src/Cleansia.Core.Domain/Users/UserConsent.cs` —
  per-user grant/withdraw with `GrantedAt` / `WithdrawnAt` / `IpAddress` / `UserAgent`. Cleaners are
  `User`s (`UserProfile.Employee`), so they already carry these rows.

So the platform has a real consent posture, and the question in front of the owner is not manners but
which basis covers *disclosing a worker's hour-by-hour occupancy to a third party for that third
party's convenience*. On legitimate interest the balancing test has to survive the CH-D1 volume, not
the one-slot story. On contract-necessity it has to be in the cleaner's terms before the feature ships.

**Where it becomes mechanism, not text.** If the owner's answer is "cleaners may object / opt out"
(the most likely outcome of any balancing test on worker data), the busy check must honour a
per-employee suppression flag. And then the rendering is a genuine design constraint that the ADR
should settle now, because two of the three options are wrong:
- `false` (mark unavailable) for a suppressed cleaner **leaks the opt-out itself** — the classic
  opt-out-reveals-membership disclosure, and it silently costs that cleaner every preferred booking.
- `true` asserts something the server declined to evaluate.
- `null` is the only honest value — *not evaluated* — and D5's contract already says `null` renders as
  an ordinary unmarked selectable row.

**Ask:** D5's `null` doc-comment should name suppression as a third `null` producer alongside "no slot
in the request" and "the check could not run", so the tri-state's third state is reserved for it. That
is a comment today and a working seam tomorrow, and it costs nothing. Then D7.4's claim that the answer
"changes text, not the mechanism" becomes true instead of assumed.

---

## What I checked and found sound

Silence is not assent, so these are named explicitly. Each died at a guard check, and I am recording
the guard.

1. **The "has served this customer" restriction is real, and the set is NOT cheap to expand — the ADR
   is right about this.** `GetMyServingCleaners.cs:27-28` requires `o.UserId == userId &&
   o.CurrentStatus == OrderStatus.Completed`; the write-side twin `UserHasCompletedOrderWithEmployeeAsync`
   (`OrderRepository.cs:294-305`) applies the identical predicate. **Booking-and-cancelling does not
   work**: `Cancelled` is terminal and not `Completed`. Reaching `Completed` requires a cleaner to take
   the job and finish it (photo-gated), which the customer cannot force. More importantly, **a customer
   cannot target a specific cleaner**: assignment comes from the open board, and the one targeting
   mechanism (`PreferredEmployeeId`) is itself gated on already-completed membership
   (`CreateOrder.cs:140-154`) — circular by construction. So the set accumulates by luck, one paid
   cleaning at a time. That is a genuine structural limit and my CH-D5 deliberately does **not** claim
   otherwise.
2. **S1 holds.** `GetMyServingCleaners.cs:25` — `userSessionProvider.GetUserId()!`; the controllers
   (`OrderController.cs:187-190`) construct the query and pass no id. D4/A12 correctly refuse a
   client-supplied `estimatedMinutes`. My CH-D2 is about the *derivation inputs*, not about trusting a
   client-sent duration — the ADR is right on that narrower point.
3. **S2 holds, and the audience is genuinely narrowed.** `[Permission(Policy.CanViewPagedUserOrder)]`
   is present on both hosts. It maps to `PhysicalPolicy.Authenticated` (`PolicyBuilder.cs:18`), but the
   customer hosts pin issuance to `UserProfile.Customer` + `JwtAudiences.Customer`
   (`Web.Customer/Controllers/AuthController.cs:109`, `Web.Mobile.Customer/Controllers/AuthController.cs:113`,
   enforced on refresh at `RefreshToken.cs:91`; audience validated at
   `Web.Customer/Extensions/ServiceExtensions.cs:147`). `UserProfile` is a single value
   (`UserProfile.cs`), so a partner token cannot reach this endpoint. **No partner or admin can call
   it.** (My CH-D1's audience point is about *non-Plus customers*, not about roles.)
4. **S4 — the `FullName` disclosure adds no new identifier, and I withdraw the objection I started
   with.** `GetMyServingCleaners.cs:47` returns first+last, which is outside `security-rules.md:107`'s
   "cleaner first-name" exception — but the same customer already receives `FullName` **and
   `PhoneNumber`** for the same cleaner via `AssignedEmployeeDto`
   (`Features/Orders/DTOs/AssignedEmployeeDto.cs:13-18`) on the very order that put that cleaner in the
   set. The picker is strictly a subset. (`AssignedEmployeeDto`'s `PhoneNumber` is a separate question
   for a separate ticket; it is not ADR-0039's to answer.) No `TenantId`, no `UserId`, no Stripe id, no
   hash in the picker response — S4 otherwise clean, and the new field is a `bool?`.
5. **Old shipped Android clients will NOT break on the added response field** — D5's "the shipped
   clients keep working unchanged" is correct, and I checked rather than assumed.
   `customer-app/.../core/auth/AuthModule.kt:39-42` — `Json { ignoreUnknownKeys = true; isLenient =
   true; explicitNulls = false }`. iOS decodes via `Codable` with explicit `CodingKeys`
   (`CleansiaCustomerApi/Models/GetMyServingCleanersResponse.swift:25-29`), which ignores unknown keys.
   No S9 deserialization break. (`explicitNulls = false` also means the new optional *request* fields
   are omitted rather than sent as `null` — fine.)
6. **Probing the past is largely dead, by accident but effectively.** A probe at a historical instant
   is answered against `SlotBlockingStatuses` (`OrderRepository.cs:263-270` — `New`, `Pending`,
   `Confirmed`, `OnTheWay`, `InProgress`), and past work is `Completed`/`Cancelled`, i.e. terminal. So
   the oracle leaks the **future** board, not employment history. Caveat the ADR itself supplies: stale
   non-terminal rows (T-0401's un-completable in-progress order) would read busy forever, which is a
   permanent false positive on one cleaner, not a history leak.
7. **The `null`-`CurrentStatus` fail-closed fallback is preserved verbatim in D3's predicate**
   (`OrderRepository.cs:277-290` → D3's sample), and D3.2's convergence keeps one definition of
   "occupied". Both directions correct: fail-**closed** on the write gate, and the picker inherits the
   conservative answer (over-greying), which is the right asymmetry for a display feature.
8. **D8's race ruling creates the order and stores the preference** — no new customer-facing error, no
   push, no hold. Consistent with ADR-0036 D6/A8, and it does not disclose the loss. Correct, and I
   found nothing to attack: the alternative (telling the customer they lost a race they did not know
   they were in) is a disclosure *increase* dressed as courtesy.
9. **No new endpoint, no migration, no new index, and no partner/admin surface** — the ADR's "zero data
   risk" claim on the schema side checks out. The picker is reachable only from
   `Web.Customer`/`Web.Mobile.Customer` (`rg "MyServingCleaners"` returns exactly those two controllers
   plus the handler, a test, and the generated customer client).

---

## Summary for the lead

| # | Challenge | Class | Blocking? |
|---|---|---|---|
| CH-D1 | No rate limit on `MyServingCleaners`; authenticated callers exempt from the global cap ⇒ 84 requests = a week's calendar for 20 named cleaners. Not Plus-gated server-side. | **S5 FAIL** | **BLOCK** |
| CH-D2 | `SelectedServiceIds`/`SelectedPackageIds` is a range parameter under another name (seed catalog = 20.25 h). D7.4's hard line and verify #11 both pass on a violating request; D3.1's "by construction" is false. | Design | **BLOCK** |
| CH-D3 | Tri-state collapses in `OrderApi.kt:343-352` / `ServingCleanersClient.swift:19-24` — mappers the ADR never names; fail-closed ⇒ picker silently disappears. | Correctness | **BLOCK** (A4 scope + automated pin) |
| CH-D4 | Both clients fetch once; D7.1's clearing rule needs a lifecycle rewrite (A4 mis-sized), and post-rewrite probing becomes the *default* interaction, invalidating D7.4's "determined customer" framing. | Scope + disclosure | Fold into D7.4 + A4 |
| CH-D5 | The membership gate is presented as a privacy control; for the physical-safety threat it selects the highest-risk population. Reduces to documentation **if** CH-D1/CH-D2 are fixed. | Framing | Non-blocking if 1+2 land |
| CH-D6 | S10: picker lists deactivated/erased cleaners; the new flag renders them affirmatively "available" and a hold is granted to someone who left. | **S10 FAIL** | **BLOCK** — A17 must gate A4 |
| CH-D7 | Tenancy variants correct and **not reachable from a request path today** (verified, latent). Guard is a naming convention; ask for an arch-test + non-null-`TenantId` pins. | Latent | Non-blocking; add AC |
| CH-D8 | `BookingPolicy` is in AppServices; Infra.Database cannot reference it ⇒ D3 doesn't compile, and the cheap fix is a hard-coded safety bound feeding the `TakeOrder` write gate. | Correctness | Sequence before A1 |
| CH-D9 | `Q-AVAIL-04` scoped as notice; the real question is lawful basis, and an opt-out outcome changes the mechanism. Reserve `null` for suppression now. | Escalation scope | Non-blocking; amend D5 comment |

**Bottom line.** The ADR is unusually honest about *what* it discloses and I have not been able to
break that description. What I can break is the claim that the disclosure is **bounded**. D5's two
"structural limits" are one real limit (the serving-cleaner set — verified, holds) and one that does
not exist (one instant). Restore the second one — a real per-`sub` rate limit and a server-fixed window
— and D7.4's acceptance argument becomes sound as written. Ship it as drafted and the ADR's own
compliance checklist will certify a schedule feed as "one instant, never a calendar."
