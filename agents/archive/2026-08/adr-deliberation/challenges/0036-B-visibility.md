# Challenger B — ADR-0036, the visibility seam

**Mode:** challenger (not author, not lead). **Posture:** REFUTED-by-default. Every claim below cites
`file:line` I opened in the working tree on 2026-08-02. Nothing was run — no build, no test, no query.

**Verdict summary:** the *shape* (stored absolute deadline, one Domain expression, no expiry actor) is
right and I could not break it. The *enumeration it rests on* is wrong in four separate ways, and three
of those are blocking. In particular the ADR's headline safety claim — *"an order stuck held is not
expressible"* (`0036:124-126`, `:561-562`) — is **false as the expression is written**, and I can reach
the stranded state two ways from shipped code.

Blocking: **CH-V1, CH-V2, CH-V3, CH-V5.** Non-blocking but must be answered in writing: CH-V4, CH-V6,
CH-V7, CH-V8. Attacked and failed: CH-V9 (the lifecycle attack the PM asked for — it does not land, and
here is exactly what I checked).

---

### CH-V1 — The hold predicate as written strands an order for anyone when `PreferredEmployeeId` is null and the deadline is live — and I can reach that state twice from shipped code

**The hole.** D5's expression (`0036:267-270`) is

```
o.PreferredHoldUntilUtc == null || o.PreferredHoldUntilUtc <= nowUtc || o.PreferredEmployeeId == employeeId
```

There is no branch for `PreferredEmployeeId == null && PreferredHoldUntilUtc > now`. In that state every
disjunct is false for **every** caller: the order is invisible on the list, invisible on the preview,
un-browsable on detail, and un-takeable, for up to 12 hours, with **no actor able to clear it** — because
the ADR's own D2.1 forbids any `UPDATE`, sweep, or job (`0036:123-126`; verify #1, `0036:601-604`).

That is precisely the outcome D2.1 and the Consequences section say the design makes *inexpressible*.
It is expressible. It is one null away.

**Why it matters — the state is reachable, not hypothetical.**

1. `Order.AnonymizeCustomerData()` (`src/Cleansia.Core.Domain/Orders/Order.cs:613-626`) sets
   `PreferredEmployeeId = null` at `:621` and touches nothing else on the pair. The ADR relegates the
   companion null to a parenthetical (`0036:143-144`) and a review checklist item (verify #1). So the
   design's headline safety property is enforced by *a reviewer remembering*, not by construction —
   which is exactly backwards for a claim of the form "not expressible".
2. `Order.Create` (`Order.cs:328-353`) independently null-collapses the id at `:349`
   (`PreferredEmployeeId = string.IsNullOrEmpty(preferredEmployeeId) ? null : preferredEmployeeId`)
   while the deadline would be assigned separately by `OrderFactory`. Two fields, two writers, one
   invariant, no owner.

**What I want changed (blocking, 12 characters + one role edit).**

- Add the missing disjunct: `|| o.PreferredEmployeeId == null`. Then a null beneficiary means "no hold,
  ever" **by construction**, no future writer of `PreferredEmployeeId` can strand an order, and the
  anonymizer's null-out becomes tidy-up rather than load-bearing. It also makes D2.2's actual claim
  ("the predicate keys on the deadline") true in the degenerate case instead of only in the happy one.
- Move the pair onto the aggregate: `Order.GrantPreferredHold(string employeeId, DateTime untilUtc)`
  and `Order.ClearPreferredHold()`, with `PreferredHoldUntilUtc` having no independent setter. The
  responsibility *"the hold is a pair, never two fields"* belongs to `Order`; the ADR currently leaves
  it in `OrderFactory`, which is why it fell through. Record it on the role card's "does NOT know" list:
  **`OrderFactory` does not know how to set a deadline without a beneficiary.**

---

### CH-V2 — D5's stated wiring does not work: `CreateAvailableOrdersSpec` never sets `RestrictToEmployeeId`, so surfaces 2 and 6 stay open while §verify #2's grep goes green

**The hole.** `0036:273-275` says the new term is *"a new `NotHeldFromEmployeeId` term, ANDed alongside
`RestrictToEmployeeId` at `:134-139`, which makes surfaces 1 **and** 2 correct at once."*

That is false. `RestrictToEmployeeId` has exactly **one** setter in the whole solution:

- `src/Cleansia.Core.AppServices/Features/Orders/GetPagedOrders.cs:91` — `restrictToEmployeeId: isAdmin ? null : callerEmployeeId`.

`DashboardSpecifications.CreateAvailableOrdersSpec` (`DashboardSpecifications.cs:8-29`) calls
`OrderSpecification.Create(...)` with 17 named arguments and **omits `restrictToEmployeeId` entirely**
(it is a defaulted parameter, `OrderSpecification.cs:150`). It passes `excludeEmployeeId: excludeEmployeeId`
at `:27` instead. So an implementer who follows D5 literally puts the hold term inside
`if (!string.IsNullOrEmpty(RestrictToEmployeeId))` at `OrderSpecification.cs:134` and:

- surface 2 (`GetAvailableJobsPreview.cs:50`) keeps leaking,
- **surface 6, which the ADR never names** (`GetDashboardStats.cs:236` → the same
  `CreateAvailableOrdersSpec`, feeding `DashboardStatsDto.AvailableOrdersCount`), keeps leaking,
- and §verify #2 (`0036:605-608`) **passes** — `NotHeldFromEmployee` does appear in `OrderSpecification`.

A green grep over broken wiring is worse than no grep. It is the exact failure mode the "one expression"
idea was introduced to prevent, reproduced by the verify item that was supposed to prevent it.

**Second trap in the same call site.** `CreateAvailableOrdersSpec(string excludeEmployeeId)` already
carries the caller's own employee id under a name that means the **opposite polarity**:
`ExcludeEmployeeId` compiles to `x.AssignedEmployees.All(ae => ae.EmployeeId != ExcludeEmployeeId)`
(`OrderSpecification.cs:129-132`) — "orders I am *not* on". The hold term needs the same id with the
inverse sense — "orders held *for me* are visible". Reusing that one parameter for both is how the next
reader inverts it. Give the hold term its own named parameter.

**What I want changed (blocking).**

1. D5 must specify a **new independent property with its own `if` block** on `OrderSpecification`
   (`NotHeldFromEmployeeId` + `NowUtc`), *not* a term inside the `RestrictToEmployeeId` block.
2. D5 must name `CreateAvailableOrdersSpec`'s **signature change** and both of its callers
   (`GetAvailableJobsPreview.cs:50`, `GetDashboardStats.cs:236`) as part of the ticket. Note the cost the
   ADR does not price: `OrderSpecification.Create` is a **19-parameter positional factory**
   (`OrderSpecification.cs:144-172`); a 20th parameter touches all four production call sites
   (`DashboardSpecifications.cs:10,33,59`, `GetCustomerOrders.cs:35`, `GetPagedOrders.cs:73`) plus three
   test sites (`OrderSpecificationCurrentStatusTests.cs:81,98,99`).
3. Fact A's table becomes **six** rows, and §verify #2 stops being a grep count and becomes a
   **call-site** check: *"every `OrderSpecification.Create` invoked on behalf of an employee passes
   `notHeldFromEmployeeId`; a call that omits it is a leak."* Counting hits does not prove coverage.

---

### CH-V3 — CH-3, adjudicated: it is **three** in-memory sites, not two; the repo has **zero** precedent for `.Compile()`; and "mirrors" is the thing §verify #2 calls a hard reject

This is the section the author left conceded-unresolved (`0036:813-820`). Here is the shape.

**Correction to the premise.** The author's CH-3 says *"two of the five surfaces cannot consume the
specification"*. Three cannot, as written:

| # | Surface | Evaluation today | Citation |
|---|---|---|---|
| 3 | `CanBrowseOrderAsync` | **in memory** on a loaded aggregate — `order.HasAvailableSpots`, a `[NotMapped]`-style computed property (`Order.cs:116-117`) | `OrderAccessService.cs:85` |
| 4 | `NewJobsDigestService` | queryable, but hand-rolled and **not a visibility rule at all** — see CH-V5 | `NewJobsDigestService.cs:98-114` |
| 5 | `TakeOrder.Validator.HasAvailableSpotsAsync` | **in memory** — loads with `FirstOrDefaultAsync(o => o.Id == orderId)` at `:65-68`, then evaluates `order?.HasAvailableSpots` at `:70` | `TakeOrder.cs:63-71` |

**What the repo actually owns.** Whole-solution grep for `.Compile()` across `src/**/*.cs`: **zero
hits.** The only composition machinery is `Specification<T>`'s `&`/`|`/`!` operators
(`src/Cleansia.Infra.Common/Specifications/Specification.cs:9-22`) and
`ExpressionBuilder.And/Or` + `ParameterRebinder`
(`src/Cleansia.Infra.Common/Specifications/ExpressionBuilder.cs:15-23`) — all of which produce
`Expression<Func<T,bool>>` for a queryable and nothing else. So the author's revised wording,
*"compiles or mirrors as a two-line in-memory check on the same three fields"* (`0036:817-819`),
proposes either a technique with no precedent in this codebase or a **hand-rolled copy of the
predicate**, which §verify #2 (`0036:607-608`) declares *"a hard reject."* The ADR currently
prescribes the thing its own review rule forbids.

#### The shape I want, concretely

**Step 1 — take surface 5 off the in-memory list. It does not belong there.**
`TakeOrder.Validator` already issues a fresh query per rule (`:65-68`, `:84-87`, `:150-152`). The hold
gate becomes a `MustAsync` whose *where clause* carries the expression — no materialization, no second
evaluation mode. That drops the genuine in-memory set to **one**: `CanBrowseOrderAsync`.

**Step 2 — one three-argument lambda, two derivations, zero hand copies.**

```csharp
// Cleansia.Core.Domain/Orders/OrderVisibility.cs — the ONE rule, written once.
private static readonly Expression<Func<Order, string?, DateTime, bool>> Rule =
    (o, employeeId, nowUtc) =>
           o.PreferredHoldUntilUtc == null
        || o.PreferredEmployeeId  == null                                        // CH-V1
        || o.PreferredHoldUntilUtc <= nowUtc
        || o.PreferredEmployeeId  == employeeId
        || o.AssignedEmployees.Any(ae => ae.EmployeeId == o.PreferredEmployeeId); // CH-V5b

/// Queryable form — partially applies Rule via the repo's existing ParameterRebinder.
/// No new technique: this is the same rebinding ExpressionBuilder.Compose already does.
public static Expression<Func<Order, bool>> NotHeldFrom(string? employeeId, DateTime nowUtc);

/// In-memory form — a SINGLE static compilation of the SAME tree at type-init.
/// Not a copy. Not a per-call Compile(). Params stay arguments, so there is no
/// per-(employee, clock) delegate cache and no allocation on the hot path.
private static readonly Func<Order, string?, DateTime, bool> Compiled = Rule.Compile();
public static bool NotHeldFrom(Order order, string? employeeId, DateTime nowUtc)
    => Compiled(order, employeeId, nowUtc);
```

This answers the allocation objection (one `Compile()` per process, not per call) and the drift
objection (there is no second source of truth to drift from). It introduces `.Compile()` to the
codebase, which is a real first — so it needs the ADR to say so and the catalog entry to name the
constraint: **compile the shared tree once, statically; never `.Compile()` inside a request.**

**Step 3 — the enforcement is a test, not a grep.** §verify #2 cannot detect drift and must be
demoted. Add, as a blocking test-contract item:

> **TC-PREF-EQUIV-0.** Over a fixture table covering every combination of
> `(PreferredHoldUntilUtc ∈ {null, past, future}) × (PreferredEmployeeId ∈ {null, self, other}) ×
> (beneficiary assigned? yes/no) × (callerEmployeeId ∈ {null, self, other})`, assert
> `db.Orders.Where(NotHeldFrom(caller, now)).Select(o => o.Id)` returns **exactly** the same id set as
> `allRows.Where(o => NotHeldFrom(o, caller, now)).Select(o => o.Id)`.

That is the only mechanism that catches the failure mode CH-3 names — including the null-comparison
semantics the author waved at, where the two evaluators are *not* obviously equal and nobody has proved
they are. It must run against **Postgres**, not an in-memory provider, or it proves nothing about the
translation.

**Step 4 — the "does NOT know" line.** `OrderAccessService` does not know the hold rule; it *asks*
`OrderVisibility`. If a future scenario forces it to reason about deadlines itself, the collaborator is
missing. Put that on `docs/domain/roles/preferred-cleaner-hold-resolver.md`.

---

### CH-V4 — Every order has ≥2 spots, so the hold as specified locks the **second seat** for up to 12 h *after* the perk has already been delivered — to a cleaner who cannot take it

**The hole.** `Order.CalculateRequiredEmployees()` (`Order.cs:509-522`) sets
`MaxEmployees = RequiredEmployees + 1` at `:519` for every order with `EstimatedTime > 0`, and
`OrderFactory.cs:148` calls it on **every** created order. So the normal single-cleaner job is a
**2-spot** row, and `HasAvailableSpots` (`Order.cs:116-117`) stays true after the first cleaner takes it
— which is why `RestrictToEmployeeId` (`OrderSpecification.cs:137-138`) and `CreateAvailableOrdersSpec`
(`hasAvailableSpots: true`, `DashboardSpecifications.cs:25`) still surface it, and why
`GetPagedOrders.cs:178-192` bothers to redact PII for a "non-assigned browser".

Now run the hold through it. The preferred cleaner takes the order at minute 3 of a 24-minute hold
(`TakeOrder.cs:188` adds the assignment, `:192-196` flips `New → Confirmed`). The order still has an
open spot. The expression (`0036:267-270`) still returns **false** for everyone else, because
`PreferredHoldUntilUtc` is still in the future and `PreferredEmployeeId` still is not them. Meanwhile
the preferred cleaner cannot take the remaining spot either — `NotAlreadyAssignedToEmployeeAsync`
(`TakeOrder.cs:79-90`) refuses it.

**Result: a spot that no one on the platform may take, for the remainder of the hold.** That is the
stuck-held catastrophe again, in its second reachable form, and Invariant H does not cover it: H bounds
the hold as a fraction of the fill window, but here the *entire* fill window of the second seat is
consumed by a perk that was already honoured.

There is a nastier variant on surface 2. Because `CreateAvailableOrdersSpec` filters to
`{Pending, Confirmed}` (`DashboardSpecifications.cs:24`) and a fresh order is `New`
(`OrderFactory.cs:166`), a held order **cannot appear there at all until it is taken** (see CH-V6). So
the hold term's only *observable* effect on the mobile dashboard is to hide second seats of orders whose
preferred cleaner already took the first. Exactly backwards.

**What I want changed (blocking).** Add the consumption clause to the shared expression:

```
|| o.AssignedEmployees.Any(ae => ae.EmployeeId == o.PreferredEmployeeId)
```

("the hold is consumed the moment the beneficiary is on the order"). It is a correlated `EXISTS` of the
same shape `RestrictToEmployeeId` already emits at `OrderSpecification.cs:137`, so it costs nothing new
in translation. And add the test: **TC-PREF-CONSUMED-0** — a 2-spot held order taken by the preferred
cleaner at minute 3 is visible and takeable by a second cleaner at minute 4, not at minute 25.

---

### CH-V5 — Surface 4 is not a visibility surface. D5 and D5.3 put **two different rules over the same two columns in one method**, and §verify #2 calls one of them a hard reject

**The hole.** `NewJobsDigestService` produces a **count**, not a list:
`["count"] = takeable.ToString(...)` (`NewJobsDigestService.cs:173`); the query at `:120-122` projects
`{Id, CleaningDateTime, EstimatedTime}` and never leaves the service. Nothing about a held order can
leak through it — D4's hard line (`0036:254-256`: no cleaner learns the beneficiary's identity or that
they were passed over) is untouched by a count that is off by one.

So surface 4 does not answer *"may this cleaner see this order"*. It answers *"has this order become new
to this cleaner since their watermark"* — a **freshness/targeting** question. And D5.3 (`0036:331-338`)
correctly states it as a *different* predicate:
`availableToCleanerAt = max(latest status-track CreatedOn, PreferredHoldUntilUtc)`.
That is not `NotHeldFromEmployee` and cannot be expressed as it.

**Why it matters.** D5 (`0036:274-275`) tells the implementer to apply the shared expression at
`NewJobsDigestService` **as well as** D5.3's max-rule. Two rules over the same two columns in one
method, one of which is a bespoke hand-rolled predicate — which §verify #2 (`0036:607-608`) declares a
hard reject. The ADR contradicts its own review rule, and an implementer who applies both gets the worst
outcome: a held order vanishes from the count at creation via rule A and reappears at expiry via rule B,
two mechanisms doing one job, each capable of drifting from the other.

**What I want changed (blocking, and it simplifies the ADR).** Restate Fact A as **three kinds**, not
one list of five:

| Kind | Surfaces | Rule |
|---|---|---|
| **Queryable visibility** | `OrderSpecification` (list, `GetPagedOrders.cs:91`) · `CreateAvailableOrdersSpec` (preview `GetAvailableJobsPreview.cs:50` **+ stats `GetDashboardStats.cs:236`**) | `OrderVisibility.NotHeldFrom` (queryable form) |
| **In-memory authorization** | `CanBrowseOrderAsync` (`OrderAccessService.cs:85`), consumed by `GetOrderDetails.cs:45` **and** `GetOrderPhotos.cs:58` | `OrderVisibility.NotHeldFrom` (compiled form, CH-V3) |
| **Write gate** | `TakeOrder.Validator` | `OrderVisibility.NotHeldFrom` pushed into the where clause (CH-V3 step 1) |
| **Notification freshness — NOT visibility** | `NewJobsDigestService.cs:109-114` | **D5.3's max-rule only.** The shared expression must **not** be applied here. |

Then §verify #2 becomes checkable and true, and D5.3 stops being an exception to a rule it was never
subject to.

---

### CH-V6 — Surfaces 2 and 4 have **already** diverged about what "available" means, and TC-PREF-HOLD-0 would go green for the wrong reason

**The hole.** The ADR's core structural claim (`0036:66-68`) treats the five as a homogeneous set —
*"a sixth condition added to four of five places is a leak, and to three of five is a bug."* They are
not homogeneous, and they already disagree:

- `DashboardSpecifications.cs:24` → `{Pending, Confirmed}`.
- `NewJobsDigestService.cs:52-53` → `{New, Pending, Confirmed}` — under a comment at `:49-50` that
  states it *"Mirrors `DashboardSpecifications.CreateAvailableOrdersSpec`."* **It does not.**
- `OrderFactory.cs:166` creates every order at `OrderStatus.New`.
- Whole-solution grep for `OrderStatusTrack.Create(` — the production writers are `New`
  (`OrderFactory:166`), `Confirmed` (`TakeOrder:194`, `ConfirmRecurringOrder:111`,
  `HandlePaymentNotification:261`), `OnTheWay` (`NotifyOnTheWay:98`), `InProgress` (`StartOrder:140`),
  `Completed` (`CompleteOrder:255`), `Cancelled` (`AdminCancelOrder:104`, `CancelOrder:128`,
  `StaleOrderCleanupService:46`, `CleanupStalePendingOrders:77`, `AutoCancelStaleRecurringOrders:86`,
  `HandlePaymentNotification:304`), and the admin override (`AdminOverrideOrderStatus:108`).
  **No organic production path writes `OrderStatus.Pending`.** (`OrderFactory.cs:116` passes
  `PaymentStatus.Pending` — a different enum.)

⟹ Surface 2 / surface 6 can return **only partially-filled `Confirmed`** orders. A freshly created,
unclaimed, held order is `New` and is invisible there regardless of the hold.

**Why it matters for this ADR, not just as a pre-existing bug.**

1. **TC-PREF-HOLD-0** (`0036:641-643`) asserts a held order is absent from `GetPagedOrders`,
   `GetAvailableJobsPreview` **and** `CanBrowseOrderAsync` for a non-preferred cleaner. Against today's
   code the `GetAvailableJobsPreview` assertion **passes before the fix is written**, because the order
   is `New`. A green assertion that proves nothing, inside a red-first test contract. The fixture must
   seed the order at `Confirmed` with an open spot to exercise the rule at all — at which point CH-V4
   applies and the test becomes the *second-seat* test.
2. It tells the lead what "the five" really are: an inconsistent set that has already drifted, in a
   service whose own comment asserts the opposite. That is the evidence for CH-V5's reclassification,
   and it is a stronger argument for the ADR's "write it once" instinct than the ADR itself makes.

**What I want changed (non-blocking, must be answered).** Record the `{Pending,Confirmed}` vs
`{New,Pending,Confirmed}` divergence in Fact A; correct the false comment at
`NewJobsDigestService.cs:49-50` in the same ticket (it is one line and it is actively misleading); and
re-specify TC-PREF-HOLD-0's fixture. If the panel believes surface 2 *should* show `New` orders, that is
a separate ticket for the PM — say so rather than letting the ADR inherit the ambiguity.

---

### CH-V7 — D5.2's read/write-agreement principle is violated by the gate two lines above it, and the ADR wants that principle in `patterns-backend.md`

**The hole.** The catalog paste at `0036:754-756` elevates to a platform rule: *"The refusal at the write
gate must agree with what the same caller's read returns."*

`TakeOrder.Validator` already breaks it. `HasAvailableSpotsAsync` returns
`BusinessErrorMessage.NoAvailableSpots` (`TakeOrder.cs:44-45`, `:63-71`) for a **fully assigned** order —
an order the same caller's `GetPagedOrders` does not return, because `RestrictToEmployeeId`
(`OrderSpecification.cs:134-139`) requires `assigned-to-me OR still-has-a-spot`. So the shipped code
already answers "no spots" for a row whose GET is empty: the exact violation the proposed rule forbids.

Shipping a catalog rule the codebase violates on day one is the "rule that keeps being violated" failure
mode — it either needs enforcement or needs to change, and this one needs to change.

**What I want changed (non-blocking, must be answered).** Pick one:
(a) the rule stands and `NoAvailableSpots` is a pre-existing leak → the ADR files the ticket; or
(b) narrow the rule to what is actually defensible and checkable:
> *"Never introduce an error key that names the exclusivity. Reuse the most generic refusal the caller
> could already have received."*
I recommend (b). It defends `OrderNotFound` for the held case without retroactively condemning a shipped
error, and verify #4 (`0036:612-614` — grep for any new *hold*/*reserved*/*preferred* error key) already
tests exactly (b), not (a).

**And one mechanical requirement D5.2 must state**, or the refusal leaks the wrong error:
`RuleFor(x => x.OrderId)` is `Cascade.Stop` with `ExistsAsync → OrderNotFound` at `:42-43` and
`HasAvailableSpotsAsync → NoAvailableSpots` at `:44-45`. If the hold rule is appended after
`HasAvailableSpotsAsync`, a **full** held order returns `NoAvailableSpots` — the disagreement, for the
one case the rule exists for. And that chain never resolves the caller's employee id (only the
`RuleFor(x => x)` chain at `:47-60` does, via `_orderAccessService`). So the hold rule must be placed
**before** the spots check and must be given the caller id explicitly. Name the position, or it will be
appended.

---

### CH-V8 — The stored-deadline consequences: three of four hold; the tunability one is a `const`, and "expiry has no actor" is true of the deadline but not of the mechanism

I attacked D2's four consequences (`0036:122-140`) and could break two claims, neither fatally.

**(a) "Tuning the policy cannot rewrite live orders" (`0036:132-135`) — TRUE, and it is also the
surprise the PM asked about.** After a tuning deploy two populations coexist (orders granted 10%, orders
granted 5%) with no way to tell them apart except arithmetic on `CreatedOn`. That is genuinely the right
trade — it is ADR-0009 D2 / ADR-0035 D1's freeze principle and I do not want it changed. **But the ADR
double-counts it.** CH-10's entire defence of shipping uncalibrated constants is *"both numbers are
single constants that can be tuned without touching a live order or a schema"* (`0036:871-872`) — and
D3 writes them as `public const` (`0036:155-157`), i.e. **compile-time**. A `const` in `BookingPolicy` is
not a tuning knob; it is a release, a client-sync obligation (`BookingPolicy.cs:4-5`: *"Keep mobile, web,
and backend in sync by referencing these numbers"*), and a feedback loop measured in weeks. Either the
number moves somewhere a tune is cheaper than a release, or CH-10 loses its mitigation. **Ask:** stop
counting "it's a constant" as the mitigation while writing `const`; state the tuning cost honestly
(one release, both partner clients unaffected because they never read it) and let the lead decide CH-10
on that basis.

**(b) "Expiry needs no actor" (`0036:123-126`) — TRUE of the deadline, FALSE of the mechanism.** The D4
targeted push is produced inline into the outbox (`0036:214-215`, riding ADR-0002/0008). That *is* an
actor, and it has a failure mode the ADR does not name: if the drain is delayed past
`PreferredHoldUntilUtc` on a 24-minute hold, the perk did nothing while still costing 24 minutes of board
latency. D5.1's *"no notification ⇒ no hold"* (`0036:233-235`) covers the **muted** case, not the **late**
case. This is the ADR's own argument against the digest (`0036:225-229` — *"the hold length must be set
by the customer's tolerance for latency, not by our notification cadence"*) turned on the outbox. **Ask:**
name the accepted consequence, or state the drain bound the minimum hold (24 min) must exceed.

**What holds and I could not break:** D2.2 (predicate keyed on the deadline ⇒ no legacy row acquires
behaviour, no backfill) — confirmed sound given CH-V1's fix; D2.4 (the future decline action is one
write; a per-country window is a resolver change with no schema change) — confirmed, and A4's rejection
(`0036:544`) survives: a read-time duration really would need a new column for both.

---

### CH-V9 — The lifecycle attack the PM assigned me **does not land.** "Computed once at creation" survives, and here is exactly what I checked

I was asked to break the stamped deadline on reschedule / edit / cancel-and-recreate. I could not. Named
explicitly, because silence is not assent:

- **There is no reschedule path.** `Order.CleaningDateTime` is `private set` and is assigned in exactly
  one place — `Order.Create` (`Order.cs:337`). The entity's public mutators are `UpdatePaymentStatus:396`,
  `UpdatePhone:428`, `UpdateEstimatedTime:435`, `SetCurrency:442`, `SetVatBreakdown:459`,
  `SetTravelDistance:470`, `SetMaxEmployees:524` — **no date, no address, no services**. And
  `src/Cleansia.Core.AppServices/Features/Orders/` (50 files) contains no `RescheduleOrder`,
  `UpdateOrder` or `EditOrder` command. A rescheduled order's lead time cannot change because an order
  cannot be rescheduled.
- **Cancel-and-recreate is correct by construction.** The new order runs the resolver against its own
  lead time and its own `nowUtc`; the old row's deadline is irrelevant because the predicate is
  per-row.
- **An order does not return to the board unassigned.** The only un-assign is `AdminReassignOrder.cs:86`
  (`Order.UnassignEmployee`, `Order.cs:498-507`), and the same handler re-assigns at `:98` before
  returning. `OrderAssignmentCancelled` (`NotificationEventCatalog.cs:38`) is a notification key, not a
  transition. The ADR's "open edge" at `0036:946-949` is therefore **not reachable today**.

**What I want changed (one sentence, non-blocking).** Say *"does not exist today"* rather than leaving
it an unexamined edge — because "does not exist" is what a future cleaner-side-cancellation ticket must
trip over. Add to D11 and to the role card: *any future path that un-assigns an order back to the board
must decide the hold explicitly; the default is to leave `PreferredHoldUntilUtc` untouched (long past ⇒
open), which is only safe because the deadline is absolute and not a duration from "now".* That converts
an unexamined edge into a designed one at zero cost, and it is the strongest single argument for D2 over
A4 that the ADR does not make.

---

### CH-V10 — CH-10 (uncalibrated constants): I do **not** block on it, with one condition

DEV is live and was not queried (`0036:928-930`). I agree with the author that the *shape* is right
independent of the numbers, and I will not hold an ADR hostage to a measurement that needs traffic the
platform may not have yet. Two things make shipping acceptable and one does not:

- **Acceptable:** the numbers are bounded by Invariant H at both ends, and being wrong is recoverable
  (an order that opens 24 minutes late is still 3 h 36 from its cleaning — longer than
  `BookingPolicy.ExpressLeadTimeHours = 2`, `BookingPolicy.cs:24`, the platform's own minimum viable
  lead).
- **Acceptable:** wrong numbers cannot corrupt data. `PreferredHoldUntilUtc` is additive and nullable;
  the worst case is latency, not a bad row.
- **Not acceptable as written:** the mitigation "tunable without a release" is false (CH-V8a).

**Condition of my non-block:** the ADR states the tuning cost truthfully, and the PM files the
measurement ticket **before** T-0515 starts, naming the three queries (time-to-first-assignment
distribution by lead-time bucket; count of approved+active cleaners per `WorkCountryId`; share of orders
never claimed) so the follow-up is executable rather than aspirational. A named measurement is worth more
than a calibrated guess.

---

### What I checked and found sound

Named explicitly — silence is not assent.

- **No host coupling.** `GetOrderDetails` is served by the Admin host (`AdminOrderController.cs:35-37`)
  and both Customer hosts (`Cleansia.Web.Customer/Controllers/OrderController.cs:107`,
  `Cleansia.Web.Mobile.Customer/Controllers/OrderController.cs:107`), so the `CanBrowseOrderAsync` change
  *does* land in code those hosts execute. It cannot change their behaviour:
  `CanBrowseOrderAsync` → `CanAccessOrderAsync` returns true for `Administrator`
  (`OrderAccessService.cs:37-41`) and for the order's own customer (`:49-52`), and returns false for any
  non-`Employee` role at `:54-57` before the browse branch is reached (`:78-82`). The ADR's
  "Customer/Admin hosts byte-untouched" claim is right, and right for a reason it does not state — I
  suggest stating it.
- **`GetOrderPhotos` is covered for free.** `GetOrderPhotos.cs:58` and `GetOrderDetails.cs:45` both go
  through `CanBrowseOrderAsync`, so one fix covers both. Worth adding to Fact A so nobody "fixes"
  photos separately.
- **`PreferredEmployeeId` reaches no partner DTO today** (verify #5). Whole-solution grep for
  `PreferredEmployeeId`: the only mapper-adjacent hits are the entity, its EF config
  (`OrderEntityConfiguration.cs:78`), the anonymizer, the factory pass-through
  (`OrderFactory.cs:124`), the command plumbing (`CreateOrder.cs:208,300`, `IOrderFactory.cs:67`) and
  migrations. `src/Cleansia.Core.AppServices/Mappers/OrderMappers.cs` has **zero** hits. The verify item
  is currently true and cheap to keep true.
- **D9 (keep `UserHasCompletedOrderWithEmployeeAsync`) is right, and the "two greps agree" claim
  verifies.** `OrderRepository.cs:294-305` filters `o.CurrentStatus == OrderStatus.Completed`; the
  picker feed does the same. Without it the hold really would be a customer-controlled targeting
  primitive. I attacked this and it holds.
- **D5.1's "one live-membership predicate" is substantively right**, with a citation nit:
  `UserMembershipRepository.cs:10-17` routes both `GetActiveForUserAsync` and
  `GetActiveForUserNoTrackingAsync` through the single `ActiveForUserQuery` at `:20`, so there is
  genuinely one predicate. But the ADR's D5.1 table (`0036:305`) lists `OrderFactory:76` and
  `QuoteOrder:141` under the **NoTracking** variant; both actually call the **tracking**
  `GetActiveForUserAsync` (`OrderFactory.cs:77`, `QuoteOrder.cs:142`). Fix the citation.
  Related, unpriced: the resolver would issue a **second** membership read per order creation on top of
  `OrderFactory.cs:76-77`. One extra indexed single-row read — I do not object, but the ADR claims
  "one collaborator added" (`0036:859-861`) and should say "one collaborator, one extra read".
- **`ExcludeEmployeeId` polarity and `RestrictToEmployeeId` semantics** read exactly as documented at
  `OrderSpecification.cs:28-33`; the server-pinning in `GetPagedOrders.cs:63-71,91` is genuinely
  server-derived (S1) and the hold term inherits that posture for free. No new client-controlled input
  is introduced anywhere in D5.
- **A5 (job/sweep expiry) is correctly rejected.** I tried to build the sweep and it needs a status, a
  claim, a retry and a dead-letter surface for something whose only job is to stop being true. The
  clock comparison is right.
- **The `Held → Open` edge really has no writer** — subject to CH-V1 and CH-V4, which are the two
  states where the edge is never taken at all.
