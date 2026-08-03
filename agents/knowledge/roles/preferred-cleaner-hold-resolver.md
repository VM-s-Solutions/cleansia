# Role — `PreferredCleanerHoldResolver` / `IPreferredCleanerHoldResolver` (+ `OrderVisibility`, + `Order`'s hold pair) (CRC card)

> **ACCEPTED — this is the standard.** Introduced by **ADR-0036**
> (`agents/backlog/adr/0036-preferred-cleaner-first-refusal-hold.md`), `accepted` 2026-08-02 after a
> full defense panel (author + three challengers + lead). **Read the ADR's `## Defense` before changing
> anything here** — six of the draft's rules were broken by challengers and the surviving forms are not
> the obvious ones. **Third sibling of `CancellationPolicyResolver` and `IExpressWaiverResolver`** —
> same shape, same namespace family, same "returns a record the caller acts on" contract. Read
> `CancellationPolicyResolver.cs:14-45`, `agents/knowledge/roles/express-waiver-resolver.md` and
> `TakeOrder.cs` in full first.
>
> ⚠️ **AMENDED 2026-08-03 by owner instruction — ADR-0039** (`proposed`) partially supersedes ADR-0036
> D5.1/A6. **The resolver now checks the cleaner's slot conflict** and a busy cleaner gets **no hold and
> no push**. The *weekly cap* half of "does NOT know" **stands**. Changes below are marked
> **[ADR-0039]**.

This card covers **three** collaborating responsibilities that must not be collapsed:

| | Owns |
|---|---|
| `IPreferredCleanerHoldResolver` | **decides** whether to notify and whether to hold, and until when |
| `Order` (the aggregate) | **owns the hold pair** — it is the only writer, and it refuses inconsistent states |
| `OrderVisibility` | **answers** "is this order open to this cleaner right now", in two evaluation forms |

## Responsibility (one sentence each)

**Resolver.** Answer, **as a pure read with no side effects**, *"may this order be held exclusively for
this customer's preferred cleaner, and should that cleaner be told about it at all?"* — returning
`PreferredCleanerOutcome(NotifyPreferred, HoldUntilUtc, Reason)` with the standing invariant
**`HoldUntilUtc != null ⇒ NotifyPreferred == true`** (ADR-0036 D4.1: the notification is granted on a
**wider** predicate than the hold; *"no signal ⇒ no hold"* survives, its converse does not).

**`Order`.** Keep `PreferredEmployeeId` and `PreferredHoldUntilUtc` consistent **as a pair**, via
`GrantPreferredHold(employeeId, untilUtc)` (throws on a null/empty beneficiary) and
`ClearPreferredHold()`. `PreferredHoldUntilUtc` has **no independent setter**.

**`OrderVisibility`.** Be the one place the hold's five terms are written:

```
open  ⟺  PreferredHoldUntilUtc == null          // never held
      ∨  PreferredEmployeeId  == null           // inconsistent pair -> fail OPEN  (CH-V1)
      ∨  PreferredHoldUntilUtc <= nowUtc        // expired, with no actor
      ∨  PreferredEmployeeId  == employeeId     // the beneficiary
      ∨  AssignedEmployees.Any()                // consumed: first seat only        (CH-V4)
```

## Collaborators

- `IUserMembershipRepository` — the **one** live-membership predicate
  (`UserMembershipRepository.ActiveForUserQuery:20-31`, reached by both the tracking and no-tracking
  methods). It creates no second predicate; the same predicate already serves
  `CancellationPolicyResolver:32`, `OrderFactory:77`, `QuoteOrder:142`, `CreateRecurringBooking:84-85`.
  **`PastDue` is excluded** — ✅ **settled by owner ruling 2026-08-03 (`Q-PLUS-05`): no benefits, cut on
  the first payment failure, no grace window.** No longer an escalation, and still not a local choice —
  this resolver adds **nothing** for that case; the shared predicate answers it.
- `IEmployeeRepository` — `ContractStatus` and `WorkCountryId` for the preferred cleaner.
- `IUserNotificationPreferencesRepository` — whether `NotificationCategory.NewJobsAvailable` is muted
  (default-allow when the row is absent, matching `NewJobsDigestService.cs:155-158`).
- **Device reachability** — ≥1 `Device` with `NotificationsEnabled == true` (`Device.cs:14-20`). This is
  the second and third of the platform's **three** ways not to receive a push; checking only the
  category mute makes D4's own rule false (ADR-0036 CH-P4).
- `BookingPolicy.ComputePreferredHold(cleaningUtc, nowUtc)` — the pure window function. Floor is
  `2 * StandardLeadTimeHours`; fraction `0.10`; ceiling `12 h`.
- **[ADR-0039] `IOrderRepository.GetBusyEmployeeIdsInWindowAsync(ids, startUtc, endUtc, ct)`** — the
  slot-conflict answer. **The picker (`GetMyServingCleaners`) calls the same method with the same
  window**; if the two ever diverge the feature has already failed. **Never** call
  `HasOverlappingOrderAsync` in a loop, and **never** pick tenancy for the caller — the scoped variant
  is correct on every request path (including the materializer, which sets a per-template
  `SetTenantOverride`); only `NewJobsDigestService` wants the ignoring sibling.
- **[ADR-0039] `OrderDuration.EstimateMinutes(services, packages)`** — the **one** definition of how
  long a booking is, shared with `OrderFactory` (which persists it as `Order.EstimatedTime`). The
  resolver's window is `[cleaningUtc, cleaningUtc + that)`. A nominal window is wrong in both
  directions; a **client-supplied** duration is an S1 violation.
- Callers: `OrderFactory` (one call, at creation) and, through it, `CreateOrder` and
  `MaterializeRecurringBookings`. The factory calls `Order.GrantPreferredHold` with the answer — it
  **never** assigns either column itself.
- Consumers of `OrderVisibility`, by **kind** (ADR-0036 Fact A, rebuilt by the panel):
  | Kind | Where |
  |---|---|
  | queryable visibility | `OrderSpecification` (its **own** `if` block) → `GetPagedOrders.cs:91`; `DashboardSpecifications.CreateAvailableOrdersSpec` → `GetAvailableJobsPreview.cs:50` **and** `GetDashboardStats.cs:236` |
  | in-memory authorization | `OrderAccessService.CanBrowseOrderAsync:85` → `GetOrderDetails.cs:45` **and** `GetOrderPhotos.cs:58` |
  | write gate | `TakeOrder.Validator`, **inside** the `ExistsAsync` rule (`:42-43`) |
  | notification | `NewJobsDigestService` — as a **conjunct**, alongside its own freshness rule |

## Does NOT know

- **How to assign an order.** Nothing in this role assigns anyone anything. `TakeOrder` remains the only
  path by which an order acquires a cleaner (ADR-0036 AC3). If a scenario needs this role to place a
  cleaner on an order, the responsibility is wrong.
- **How to write.** The resolver never sets `PreferredHoldUntilUtc`; it returns a value the factory hands
  to the aggregate. It never commits, never issues SQL, never enqueues a notification.
- **How to expire a hold.** There is nothing to expire — expiry is `now >= PreferredHoldUntilUtc` in a
  `WHERE` clause, and consumption is a row appearing in `OrderEmployees`. **If anything ever needs to
  "release" a hold on a schedule, the shape is wrong.** (The two sanctioned writes are a cleaner-side
  *decline* and a future return-to-board path, both of which call `Order.ClearPreferredHold()`.)
- **`TakeOrder`'s WEEKLY ORDER LIMIT** (`TakeOrder.cs:125-143`) — **deliberately not consulted, and this
  half is settled on evidence.** `GetEmployeeOrderCountThisWeekAsync` (`OrderRepository.cs:247-258`)
  derives its window from **`DateTime.UtcNow.Date`** (`:249-252`), so at creation, for a booking more
  than a week out, it answers about a week that **does not contain the booking**. A creation-time cap
  check is not a wrong answer — it is an answer to a different question. **Adding it is a finding.**
  A hold never overrides a `TakeOrder` gate.
- **[ADR-0039] ~~The TIME CONFLICT~~ — this is now KNOWN, and knowing it is the point.** The resolver
  **does** consult the cleaner's slot conflict at the booking's own date and time, via
  `IOrderRepository.GetBusyEmployeeIdsInWindowAsync` — **the same call the picker makes, with the same
  window.** Busy ⇒ `HoldDeclineReason.CleanerBusyAtCleaningTime` ⇒ **no hold AND no push** (D5.1's own
  rule: *"a hold for a cleaner `TakeOrder.cs:53` would reject is pure latency — and so is a push"*).
  What the resolver still does **not** know: whether the cleaner will *become* busy after creation.
  That direction is unknowable and stays bounded by Invariant H, exactly as ADR-0036 D5.1 says.
- **Whether a preference is *eligible*.** `UserHasCompletedOrderWithEmployeeAsync` lives in
  `CreateOrder.Validator` (`:150-154`) and stays there. This role assumes the preference is already
  legitimate.
- **What "new to this cleaner" means.** That is `NewJobsDigestService`'s own freshness rule (ADR-0036
  D5.3) and it is a **different question** from visibility. The digest is the one surface that carries
  both; nothing else may hand-roll either.
- **What the customer is told.** The customer sees no hold **state** in flight (ADR-0036 D6); they are
  told once, at the moment of choosing, in a sentence that must be true whether or not the hold was
  granted (§Copy constraint 8). Wording belongs to T-0491.
- **The country's time zone.** Everything here is UTC instant arithmetic; no local calendar is involved.
  (Contrast `ExpressWaiverResolver`, which needs `CountryConfiguration.TimeZoneId`.)
- **Which tenant.** The global query filter scopes its reads.
- **`OrderAccessService` does not know the hold rule** — it *asks* `OrderVisibility`. If a future
  scenario forces it to reason about deadlines itself, the collaborator is missing.

## Invariants a reviewer checks

1. **Zero writes in the resolver.** Grep the implementation for `Add` / `Commit` / `ExecuteSql` /
   `Notify` — none.
2. **`PreferredHoldUntilUtc` has no independent setter, and `OrderFactory` never touches either column.**
   The only writers are `Order.GrantPreferredHold` / `Order.ClearPreferredHold`. A bare
   `PreferredEmployeeId = null` in `AnonymizeCustomerData` is a **hard reject** — it recreates the
   stranded state the panel found (CH-V1).
3. **All five terms are present.** Missing `PreferredEmployeeId == null` (a hold nobody can act on and
   nobody can clear) or missing `AssignedEmployees.Any()` (the spare seat locked after the perk was
   delivered) each reintroduces a stuck-held state. **Hard reject.**
4. **`null` deadline ⇒ always open.** Every legacy row and every order without a granted hold is visible
   to everyone. `TC-PREF-LEGACY-0`.
5. **Six surfaces, checked as CALL SITES, not as grep hits.** `OrderSpecification.Create`'s parameters
   are all optional, so a caller that omits the new argument **compiles green and leaks**. Every
   `Create` invoked on behalf of an employee passes it; `CreateAvailableOrdersSpec`'s **both** callers
   pass it. A hand-rolled copy of the rule anywhere is a hard reject.
6. **`TC-PREF-EQUIV-0` exists and runs against PostgreSQL.** The queryable and in-memory forms are
   pinned by a test, not by review — and specifically for `callerEmployeeId == null`, where SQL's
   `= NULL` (UNKNOWN) and C#'s `null == null` (true) disagree. **No `.Compile()` on a request path.**
7. **The hold floor is `2 * BookingPolicy.StandardLeadTimeHours`** — a derivation of the one urgency
   constant. A literal `8` (or a bare `4`) is a finding.
8. **No hold without a signal — and reachability means all three checks** (category mute, device kill
   switch, no device row). The converse does **not** hold: a declined hold may still notify (D4.1).
9. **The refusal names no exclusivity, and sits inside the existence rule.** `TakeOrder` returns
   `BusinessErrorMessage.OrderNotFound`; placed after `HasAvailableSpotsAsync` a full held order leaks
   `NoAvailableSpots`. No new partner-facing error key mentioning a hold, a reservation or a preference.
10. **`PreferredEmployeeId` reaches no partner-facing DTO.**
11. **The digest's freshness clause is a bounded disjunction** — no `CASE`, no `GREATEST`, no scalar
    `(SELECT max(...))`, a mandatory `<= sweepStartedAt` upper bound, and `sweepStartedAt` (never
    `UtcNow`) as `nowUtc`.
12. **No index on `PreferredHoldUntilUtc`, and a partial index on it is a hard reject** (ADR-0036 D5.5).
13. **[ADR-0039] The busy check runs LAST and costs one set-based query.** A `HasOverlappingOrderAsync`
    call inside a loop is a hard reject; so is a busy check placed *before* the membership / lead-time
    gates (it pays a range scan for every non-member). `CleanerBusyAtCleaningTime` ⇒ **`NotifyPreferred
    == false`** — placing it beside `ShortLeadTime` (notify, no hold) is a finding: *short lead means we
    cannot hold; busy means they cannot take.*
14. **[ADR-0039] The emitted SQL carries the scan floor.** `"CleaningDateTime" >= @floor`
    (`windowStart − BookingPolicy.MaxOrderSpanHours`). Absent ⇒ the query is the old unbounded
    lifetime scan wearing a new name. **Hard reject.**
15. **[ADR-0039] The booking is never failed for this.** No answer (query error, no slot) ⇒ **no hold,
    no push, order created, preference stored**. A `CreateOrder` rejection on a busy preferred cleaner
    is a hard reject — ADR-0036 D8's rule: *reject where someone can react; degrade where nobody can.*

## Watch-list

- **`OrderFactory` is accumulating resolvers** — discounts, VAT, the ADR-0035 express waiver, now this.
  A **third** resolver on the factory should trigger a look at the factory itself rather than a fourth.
  Flagged in ADR-0036 CH-9 as a watch-list item, not a blocker. Honest cost of this one: **one
  collaborator and one extra indexed single-row read** per order creation.
- **The two constants are uncalibrated, and they are `const`.** `PreferredHoldFraction = 0.10` and
  `PreferredHoldCeilingHours = 12` are reasoned defaults. **"Tunable without a release" is false**
  (CH-V8a — `BookingPolicy.cs:4-5` exists to keep clients in sync on these numbers). The honest cost is
  **one backend release, no client change**, because no client reads the hold constants. A measurement
  ticket is a precondition of T-0515.
- **The minimum hold must stay longer than the outbox drain.** The signal rides ADR-0002/0008; a drain
  that lags past a 48-minute deadline makes the perk do nothing while still costing board latency. If
  the drain's p99 approaches it, **the floor moves, not the mechanism.**
- **Any future path that returns an order to the board unassigned must decide the hold explicitly.**
  Under term 5, un-assigning back to an empty order with a still-future deadline **re-arms** the hold.
  Not reachable today (CH-V9: no reschedule command; the only un-assign re-assigns in the same handler)
  — and `ClearPreferredHold()` is the one-call answer when it becomes reachable.
- **If a per-country window is ever wanted**, it belongs in this resolver reading `CountryConfiguration`
  (the ADR-0017 seam), **never** as a country-code branch and **never** as a per-plan number (a longer
  hold is worse for fill rate, so it is a bad upsell lever).
- **The perk's reach is bounded by push adoption.** The partner web SPA registers no devices, so a
  favourite who works from the web board gets neither push nor hold — correct under "no signal, no
  hold", but a product fact the copy must not contradict.
