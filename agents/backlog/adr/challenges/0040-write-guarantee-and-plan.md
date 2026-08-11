# Challenge — ADR-0040 (`Order.CurrentStatus` non-nullable)

**Challenger, two lanes: A `write-guarantee` · B `query-plan`.** Reviewed against the **shipped**
implementation (column is `NOT NULL`, migration regenerated in place, fallbacks deleted), not against
the ADR's description of it.

**Headline verdict: no Lane A blocker.** I hunted for a reachable production path that persists an
`Order` without a status track and did not find one. §2 stands; the fallback does **not** have to come
back; nothing must be reverted. What I did find is that (1) the residual §3 calls "benign" is
**fail-open onto the dispatch board**, not benign, and there is a committed fixture that demonstrates
it; (2) the `Initial` regeneration is a **silent no-op** on any already-migrated database and the
green test suites structurally cannot detect that; and (3) §5's *"Claimed (structural, certain)"* half
is over-stated — it is false at one of the two sites §4.1 names, and the ADR contradicts itself about
which. The §5 *disclaimer* is honest; the §5 *claim* is not.

Ordering note: **CH-W3 + CH-P3 together are the only urgent pair** — they are a pre-deploy gate, not a
pre-merge one.

---

## Lane A — the write-time guarantee

### CH-W1 — The residual is not benign. It is fail-OPEN onto the partner board, and the test suite already contains a committed instance of it.

**The hole.** §3 names one residual — `OrderStatus.New = 0` (`src/Cleansia.Core.Domain/Enums/OrderStatus.cs:8`),
so a `Create`d-but-untracked `Order` reads `New` instead of `null` — and characterises it as
*"trades a crash for a benign default"* over a window that is *"~75 lines inside `OrderFactory` … and
is unreachable by any consumer"*. Both halves of that are too generous.

**Why it matters — with the constructive case.**
`src/Cleansia.Tests/Features/Orders/OrderSpecificationCurrentStatusTests.cs:62-63`:

```csharp
// No history at all: excluded from every status filter under both the old and new rule.
ctx.Add(NewOrder("spec-no-history"));
```

`NewOrder` (`:142-161`) is a bare `Order.Create` with no `AddOrderStatus`, and it is committed at
`:65`. Under the new rule that comment is **false**: the row persists with `CurrentStatus = 0 = New`.
It is not "excluded from every status filter" — it is now *selected* by a filter on `{New}`.

The test only passes because `New` is absent from the theory data (`:70-74` covers
`[Pending,Confirmed]`, `[Cancelled]`, `[Completed]`, `[InProgress]`) and because
`ExpectedIdsFromHistoryAsync` (`:114-128`) derives expectations by grouping `OrderStatusTrack` rows,
so a track-less order can never enter `expected`. Add `[InlineData(OrderStatus.New)]` and the
assertions at `:89-90` fail: `expected` = ∅ (no seeded order's *latest* track is `New`), `actual` =
`{"spec-no-history"}`. **The coverage hole sits exactly over the ADR's named residual.**

And the consequence is not "reads `New`". It is *offerable*:

- `OrderAvailability.OfferableStatuses = [New, Confirmed]` (`src/Cleansia.Core.Domain/Orders/OrderAvailability.cs:40-41`)
- `IsOfferableSql` admits `CurrentStatus == New && PaymentType == Cash` (`:49-52`), and `IsOfferable`
  the same in C# (`:60-63`)
- both are composed into the partner available board via `OrderSpecification.cs:149` / `:162` and
  `DashboardSpecifications.CreateAvailableOrdersSpec` (`src/Cleansia.Core.AppServices/Features/Dashboard/DashboardSpecifications.cs:23-47`)

So a status-less row that reaches the database is indistinguishable from a genuine new cash booking:
it appears on the board, it is counted by the dashboard hero stat, and `TakeOrder`'s gate accepts it
(`src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs:105-110`). The **old** behaviour was
fail-closed in both directions — `!= null` excluded it from every read surface, and `OrderMappers`
threw an NRE. The trade is therefore *a loud fail-closed crash → a silent fail-open dispatch*, which
is the exact asymmetry ADR-0037 §D3 was written to preserve.

**What I want changed.** Not a revert. Three things:

1. §3's "trades a crash for a benign default" is **rewritten** to name the direction: fail-closed →
   fail-open, on the dispatch board. The residual can stay accepted; it cannot stay mis-described,
   because that sentence is what a future reader uses to decide the residual doesn't need closing.
2. The deferred durable fix (move the `New` track into `Order.Create`) is **filed as a ticket
   referenced from the Verdict**, not left as "file it, don't smuggle it" with no filer. The ADR's
   stated reason for deferring — *"it would double-write a track at ~60 test call sites … and
   renumber `Sequence`"* — is a test-churn argument, not a design argument, and it is now the only
   thing standing between the codebase and a fail-open default.
3. `OrderSpecificationCurrentStatusTests` is fixed **either** by giving `spec-no-history` a track (and
   renaming it) **or** by adding `[InlineData(OrderStatus.New)]` and letting it fail until the
   residual is closed. Leaving a fixture whose comment asserts the opposite of what the code does is
   how the next person concludes the residual is unreachable.

*Blocking?* **No.** I could not reach it from production (see "found sound" below). It downgrades
§3's risk framing, it does not falsify §2.

---

### CH-W2 — §8 item 8's compensating control did not ship, and the invariant the whole ADR rests on has zero automated defence.

**The hole.** §8 item 8 promises *"New rule for the catalog (`consistency.md`): a production `Order` is
created only via `IOrderFactory`"* plus a `check-consistency.mjs` ticket, with "until it lands, item 8
is a reviewer's grep."

**Why it matters.** Neither exists. `agents/knowledge/consistency.md` contains **zero** occurrences of
`IOrderFactory`, `OrderFactory` or `Order.Create` (searched). And no test anywhere pins the guarantee:
there is no assertion that `OrderFactory.CreateAsync` appends the `New` track *before*
`orderRepository.Add`. The load-bearing invariant of this ADR — "no path can persist an `Order`
without a status" — is defended today by exactly one thing: a reviewer remembering to run the §8 item
1 grep, which has a known false positive (CH-W4).

That is the standard this codebase explicitly rejects, in the same aggregate:
`src/Cleansia.Core.Domain/Orders/Order.cs:418-423` — *"a safety property defended by a reviewer
remembering to null the companion field is not a safety property."* The identical argument applies to
"a reviewer remembering that `Order.Create` may only be called from `OrderFactory`."

Worse, `agents/knowledge/consistency.md:310` currently asserts the **opposite of the shipped code**:
> "the read surfaces fail CLOSED on a NULL `CurrentStatus`, the take gate deliberately does not"

The living decision doc was handled correctly — `agents/architecture/decisions/order-availability.md:211-238`
carries a dated "BEING SUPERSEDED BY ADR-0040" box. `consistency.md` did not get the same treatment,
and it is the file developer agents read first.

**What I want changed.** The catalog edit and the enforcement ticket land **with the Verdict**, not
after it: (a) `consistency.md:293-314` corrected so it no longer states a fail-closed-on-NULL rule
that the code does not implement; (b) the new "`Order.Create` outside `OrderFactory.cs` + the four
test projects is a violation" rule added there with a `check-consistency.mjs` ticket filed; (c) one
test in `Cleansia.Tests` asserting that after `OrderFactory.CreateAsync` the returned order has a
non-empty `OrderStatusHistory` and `CurrentStatus == New`. Without (c), the compiler-driven migration
argument in §3 category 3 covers the *deletion* but nothing covers the *invariant*.

---

### CH-W3 — The `Initial` regeneration is a silent no-op on every already-migrated database, and neither test suite can detect it. This is the urgent one.

**The hole.** §6 treats the regeneration as a scheduled MANUAL_STEP that "rides" the owner's database
drop, and Stop condition 1 says "halt if the regeneration is not happening." That framing assumes the
failure mode is *visible*. It is not.

**Why it matters.**

- `src/Cleansia.MigrationService/Program.cs:31-41` applies **pending** migrations only:
  `GetPendingMigrationsAsync()` → if empty, print *"database is up to date — nothing to apply"* and
  `return 0`. The migration id `20260723182623_Initial` is already in `__EFMigrationsHistory` on any
  environment that has ever run it. The in-place edit at
  `src/Cleansia.Infra.Database/Migrations/20260723182623_Initial.cs:1046` (`nullable: false`) will
  **never** be applied there. It exits 0. Aspire's `WaitForCompletion` is satisfied. Nothing shouts.
- Neither test suite exercises that state. `src/Cleansia.IntegrationTests/BaseIntegrationTest.cs:74-96`
  migrates an **empty** Testcontainer; `Cleansia.Tests` builds schema from the model via
  `EnsureCreatedAsync`. Both get `NOT NULL` for free. **2798 unit + 132 integration green proves
  nothing about a database that already ran the old `Initial`.**
- The codebase is already tolerant of exactly this drift by design:
  `BaseIntegrationTest.cs:80-88` demotes `PendingModelChangesWarning` to a no-op, with a comment
  saying the model "can sit slightly ahead of the latest committed migration."
- Azure DEV is live. If it is not dropped, `Orders.CurrentStatus` stays `nullable: true` there
  indefinitely — and that is the precondition for CH-P3's silent double-booking.

Stop condition 2 (`SELECT count(*) FROM "Orders" WHERE "CurrentStatus" IS NULL`) is **necessary but
not sufficient**: a zero answer still leaves a schema that permits NULLs, so the invariant this ADR is
*about* is simply not enforced on the one environment that matters.

**What I want changed.** §6 upgrades Stop condition 1 from an intention ("the owner is dropping the
database") to a **verifiable pre-deploy gate recorded in the ADR**, discharged by evidence:

1. `SELECT count(*) FROM "Orders" WHERE "CurrentStatus" IS NULL;` → 0 (already asked for), **and**
2. `\d "Orders"` / `information_schema.columns` shows `CurrentStatus … is_nullable = NO` on every
   environment the code will run against — i.e. proof the regenerated `Initial` was actually
   *applied*, not merely committed.

Without (2), the ADR ships an invariant that exists in C# and in the migration file and nowhere in the
running database.

---

### CH-W4 — Reviewer check §8 item 1 has a false positive; it will read as a violation on a clean tree.

`rg -n "CurrentStatus\s*\?\?|CurrentStatus!\.|CurrentStatus\s*!=\s*null|CurrentStatus\s*==\s*null" src/`
is specified to return **zero hits, test projects included**. It returns exactly one:

`src/Cleansia.TestUtilities/MockDataFactories/Orders/OrderMockFactory.cs:88`
```csharp
order.AddOrderStatus(OrderStatusTrack.Create(partial.CurrentStatus ?? OrderStatus.New, order));
```

That is `OrderMockFactory.Partial.CurrentStatus` — a nullable **test-DTO** field declared at `:42`
(`public OrderStatus? CurrentStatus { get; set; }`), not `Order.CurrentStatus`. Correct code, matching
pattern.

A compliance check that is known to fire on a compliant tree stops being run. **Want:** narrow the
pattern (exclude `Cleansia.TestUtilities`, or anchor on an order-typed receiver) *or* state the one
known-good hit inline so the check stays falsifiable. §8 item 2 (`_currentStatus` → zero hits) is
clean — verified, zero hits anywhere under `src/`.

---

### CH-W5 — A stale comment of precisely the class the ADR exists to close survived, in the tests.

§4.2 rules on `OrderRepository.cs:87-89` that *"a comment asserting a mechanism that no longer exists
is the defect class ADR-0037 exists to close"*, and the implementation duly fixed it — the realized-
savings comment at `src/Cleansia.Infra.Database/Repositories/OrderRepository.cs:85-91` no longer
claims an `IS NULL` arm. The identical defect was left in the test fixture:

`src/Cleansia.Tests/Features/Orders/OrderListProjectionEquivalenceTests.cs:108-109`
> `// Same-tick Sequence tie so the NULL-column fallback subquery must apply the full`
> `// CreatedOn-desc-then-Sequence-desc rule (→ Completed); the column is NULLed after commit.`

Nothing NULLs the column any more (the raw `UPDATE` is gone — `_currentStatus` has zero hits in
`src/`), and the fixture is still named `"proj-legacy-null"` (`:111`). The *test* is right — the
same-tick Sequence tie at `:117-119` is worth keeping — only its stated mechanism is dead. **Want:**
comment + fixture id corrected in the same change as §8 item 7. Low severity, zero ambiguity.

---

### Line-number drift (mechanical, but §8 is a line-cited checklist about to become immutable)

The ADR cites pre-change coordinates throughout. On the shipped tree: `OrderFactory.cs:104 → :110`,
`:179 → :221`, `:180 → :222`; `Order.cs:407-410 → :451-456`; `OrderSpecification.cs:121-122 → :129`;
`OrderRepository.cs:291-306 → :315-330`; `OrderMappers.cs:16 → :14-17`;
`OrderEntityConfiguration.cs:111 → :109`; `Initial.cs:1042 → :1046`; `ServiceExtensions.cs:227 → :228`.
Worth a pass before `accepted`, since §8 is how a reviewer verifies compliance later.

---

## Lane B — the query-plan claim

### CH-P1 — §5's central structural claim is false at one of the two sites §4.1 names, and the ADR contradicts itself about it.

**The hole.** §5 (`ADR-0040:200-206`) asserts, under *"Claimed (structural, certain)"*:

> "Today it sits inside an `OR` with a NULL arm, so it is not an index qual at all and the only
> unconditional sargable term is a range on the **second** key column…"

§4.1 (`ADR-0040:155`) records the *actual* pre-change `OrderSpecification` term as:

> `x.CurrentStatus != null && OrderStatuses.Contains(x.CurrentStatus.Value)`

That is a **conjunction**, not a disjunction. It translates to `"CurrentStatus" IS NOT NULL AND
"CurrentStatus" = ANY(@p)` — two sargable quals on the leading column, the first of which is
redundant with the second. PostgreSQL never had to decompose anything there. The `OR` existed at
**one** site: `OrderRepository.HasOverlappingOrderAsync` (§4.1 row 2), where one arm was a correlated
subquery and therefore genuinely unindexable, which is what forced the whole predicate to a filter.

So §4.1 and §5 cannot both be true, and the ADR's own §4.1 is the one with the code in it. The claim
has already propagated into the living doc:
`agents/architecture/decisions/order-availability.md:234` now reads *"`OrderSpecification.cs:121-122`'s
`OR`-wrapped status term becomes an unconditional qual on the leading column."*

**Why it matters.** §5 is labelled *certain*. A reader banks "12 read surfaces had a structural
obstacle and now don't." One did. The other eleven got a redundant conjunct removed.

**What I want changed.** §5 rewritten to scope the structural win to the `OrderRepository` site, and
to say plainly that `OrderSpecification` gained a syntactic simplification and **not** a plan-class
change. `order-availability.md:234` corrected in the same edit.

---

### CH-P2 — The `NOT NULL` constraint is not what buys the sargability. The C# deletion is. Alternative C was dismissed on the wrong axis.

**The hole.** The ADR's title says *"the `?` is not passive (it costs an `OR` on the leading index
column at every read surface)"*, and §5 presents the non-nullable column as the enabler of the plan
change. §7 option C ("delete only the fallbacks, leave the column nullable") is dismissed as *"the
worst of both."*

**Why it matters.** `!= null` is a **C#-side** predicate. Deleting it changes the emitted SQL
regardless of the column's declared nullability. `OrderSpecification.cs:129` and
`OrderRepository.cs:329` emit exactly the same SQL against a nullable column as against a `NOT NULL`
one. **Option C therefore delivers 100% of §5's claimed plan benefit.** It was correctly rejected —
but on the *invariant* axis ("the DB still admits a state the code no longer handles", which is right
and is the real reason), not on the plan axis the title and §5 lean on.

This is provable today rather than in theory: per CH-W3, if DEV is not dropped it will run the new SQL
against the old nullable column — same plan, no constraint. The constraint and the plan are
independent, and the ADR's framing fuses them.

**What I want changed.** One sentence in §5: *the `NOT NULL` buys the invariant; the sargability comes
from deleting the C# conjunct.* And the title's parenthetical narrowed to the one surface where the
`OR` was real. The decision survives untouched — only its stated justification needs to stop
overlapping two independent benefits.

---

### CH-P3 — Deleting the correlated subquery **does** change results, and it changes them fail-open on the booking write gate. This is what makes CH-W3 urgent.

**The hole.** The brief asks: does the deletion change any *result*, not just the plan. §5 does not
address it; §4.1 presents it as *"the correlated subquery disappears entirely."*

**Why it matters.** Old form (§4.1 row 2):
`(CurrentStatus != null && SlotBlockingStatuses.Contains(…)) || (CurrentStatus == null && OrderStatusHistory…Take(1)…)`.
New form: `SlotBlockingStatuses.Contains(o.CurrentStatus)`
(`src/Cleansia.Infra.Database/Repositories/OrderRepository.cs:329`).

For a row whose column is NULL, `"CurrentStatus" = ANY(...)` evaluates to NULL → not true → the row is
**excluded**. The old second arm consulted history and could **include** it. An excluded live
commitment does not block a slot. That is a **double booking**, and it lands on the two places that
matter most:

- `HasOverlappingOrderAsync` (`:270-282`) — the booking write gate
- `GetBusyEmployeeIdsInWindowAsync` (`:284-307`) — the dispatch busy-set

Neither materialises an `Order` entity (one is `AnyAsync`, the other projects `EmployeeId` strings), so
on a drifted database the NOT-NULL/NULL mismatch will **not even raise a materialisation error** to
warn you. It is silent, and it fails open.

So "a fallback that never fired is free to delete" is conditional on there being no NULL rows
*anywhere the code runs* — which is exactly CH-W3's unverified premise. The ADR's Stop condition 2
asks the right question; it is not answered in the repo, and this is the consequence that makes it
load-bearing rather than hygienic.

**What I want changed.** Stop condition 2 is promoted from "halt the parallel implementation" (moot —
it shipped) to a **pre-deploy gate**, and its failure mode is renamed from *"it also falsifies §2"* to
what it actually is: **silent double-booking on the write gate**. That is the sentence that gets the
query run.

---

### CH-P4 — The surface ADR-0039's cost lane was actually about still has an `OR` on the leading column, and the ADR does not say so.

`DashboardSpecifications.CreateAvailableOrdersSpec` (`src/Cleansia.Core.AppServices/Features/Dashboard/DashboardSpecifications.cs:23-47`)
sets `offerableOnly: true` **and** `orderStatuses: OrderAvailability.OfferableStatuses`, so the partner
available board and its dashboard count compose `IsOfferableSql` (`OrderSpecification.cs:149`, again at
`:162` for `RestrictToEmployeeId`), which is:

```
(CurrentStatus == Confirmed || (CurrentStatus == New && PaymentType == Cash)) && (…)
```
(`src/Cleansia.Core.Domain/Orders/OrderAvailability.cs:48-52`)

That `OR` is structural to the **rule**, not to nullability, and this ADR leaves it untouched — by
design (§1 preserves ADR-0037's two-forms ruling). It is better placed than what was deleted (both
arms are equality on the leading column, so PG can `BitmapOr` two index scans rather than falling to a
filter around a correlated subquery), but *"the status term becomes an unconditional conjunct on the
leading key column"* is simply not true of the hottest partner read.

**What I want changed.** §5 names the offerability path as **not covered**, so nobody reads this ADR
as having narrowed ADR-0039's open cost question on the dispatch board.

---

### CH-P5 — The `EXPLAIN` obligation is two ADRs old, and the harness to discharge it already exists five directories away. The deferral's cost estimate is wrong.

**The hole.** §5 states ADR-0039's `EXPLAIN (ANALYZE, BUFFERS)` obligation *"survives this ADR and is
not discharged by it."* Honest — but it implies the discharge is expensive. It is not.

**Why it matters.** `src/Cleansia.IntegrationTests/Features/Memberships/UserMembershipCancellationSweepIndexPlanTests.cs`
is the **only** `EXPLAIN` in the entire repo (searched `src/` — one file) and is a complete working
template for exactly this:
own throwaway `PostgreSqlContainer` (`:29-34`) · `EnsureCreatedAsync` off the live model (`:44`) · FK
drops for isolation (`:51-53`) · a deliberately skewed seed so the planner has a reason to prefer the
index (`:134-175`) · `ANALYZE` (`:58`) · `Assert.Contains(indexName, plan)` +
`Assert.DoesNotContain("Seq Scan on \"…\"", plan)` (`:69-87`).

Meanwhile **nothing** pins query shape for Orders: `ToQueryString` has **zero** hits anywhere under
`src/`, and `src/Cleansia.Tests/Infrastructure/PerfIndexModelMetadataTests.cs:104-107` asserts only
that the index exists in the EF *model*. So §5's "structural, certain" claim has no regression test at
all — a future refactor can reintroduce an `OR` on `CurrentStatus` and every suite stays green.

**Honest limit:** I could not execute it in this session (no shell available to me), so I am **not**
discharging the obligation. But I am refuting the implicit cost estimate: this is one file modelled on
an existing one, not a research task.

**What I want changed.** A ticket referenced from the Verdict — `OrdersStatusBandIndexPlanTests`,
copied from the memberships harness — asserting over a skewed `Orders` seed that
`LiveCommitmentsInWindow`'s shape (`OrderRepository.cs:325-329`: `CleaningDateTime` range +
`CurrentStatus = ANY`) plans as an index scan on `IX_Orders_CurrentStatus_CleaningDateTime` with no
`Seq Scan on "Orders"`. One test discharges ADR-0039's obligation **and** pins §5.

---

### CH-P6 — `Contains → = ANY(@p)` on EF Core 10 + Npgsql: unverified, and the two sites are not the same shape.

§5 asserts the emitted form is `"CurrentStatus" = ANY(@p)` as part of the "certain" half. The two
call sites close over different things:

- `OrderSpecification.cs:129` — an **instance property** `OrderStatuses`, typed `IEnumerable<OrderStatus>?`
  (declared `:23`), captured from the specification object.
- `OrderRepository.cs:329` — a **`static readonly OrderStatus[]`** (`:261-268`).

EF Core 10's parameter extraction and Npgsql's primitive-collection translation do not necessarily
treat those identically: a static readonly array can be inlined as constants (`IN (0,1,2,3,4)`) while
a captured enumerable is parameterised (`= ANY(@p)`). Both are sargable on the leading column, so the
*conclusion* survives either way — but §5 states one specific emitted form as certain for both, and
nothing in the repo pins it (CH-P5: zero `ToQueryString` assertions).

There is also a direction §5 gestures at but does not connect: a **parameterised** `= ANY(@p)` is
*worse* for the planner than an inlined `IN` list, because a generic plan cannot estimate the array's
selectivity. §5's selectivity disclaimer and §5's claimed emitted form are in mild tension and the ADR
does not notice.

**What I want changed.** Soften the claim to *"a bare equality/IN on the leading column, in whichever
form EF emits"*, or pin the literal SQL in the CH-P5 test. Either is fine; asserting a specific
provider output as "certain" with no assertion behind it is not.

---

### Is §5's disclaimer honest enough?

**Yes — the disclaimer is.** It explicitly refuses the performance claim, names selectivity and set
width as unresolved, and re-asserts ADR-0039's obligation rather than quietly absorbing it. That is
the right posture and it should stay.

**No — the claim is.** Every Lane B finding above lands on the half labelled *"Claimed (structural,
certain)"*, not on the disclaimer. That is the more insidious failure: a reader who correctly discounts
the disclaimed half will still bank the certain half, and the certain half is wrong at one of the two
named sites (CH-P1), attributes the win to the wrong mechanism (CH-P2), silently excludes the hottest
surface (CH-P4), and asserts an unpinned provider output (CH-P6).

---

## What I checked and found sound

Silence is not assent, so — explicitly, this is what I verified and could not break.

**The write-time guarantee (§2), independently re-derived:**

- **W1** — `Order.Create` (`src/Cleansia.Core.Domain/Orders/Order.cs:325-391`) is a pure object
  initializer; it writes no status track. Confirmed.
- **W2** — `Order.Create` has exactly **one** non-test caller:
  `src/Cleansia.Core.AppServices/Features/Orders/OrderFactory.cs:110`. `orderRepository.Add` has
  exactly **one**: `OrderFactory.cs:222`, the line after
  `order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order))` at `:221`. Every other
  `Order.Create` / `Orders.Add` / `ctx.Add(order)` call site in the repo is in `Cleansia.Tests`,
  `Cleansia.IntegrationTests`, `Cleansia.HostTests` or `Cleansia.TestUtilities`. I re-ran the search
  independently and got the same partition.
- **W3** — `IOrderFactory.CreateAsync` has exactly two non-test callers,
  `CreateOrder.cs:402` and `MaterializeRecurringBookings.cs:155`, via the DI registration at
  `src/Cleansia.Config/Services/ServiceExtensions.cs:228`. The recurring materializer appends **no**
  second track (`MaterializeRecurringBookings.cs:155-157` — `CreateAsync` then
  `MarkMaterializedFor`), so its orders sit at `New` from the factory. Consistent; no gap.
- **W4** — `CurrentStatus` is `{ get; private set; }` (`Order.cs:308`) assigned in exactly one place,
  `Order.cs:453`. No `ExecuteUpdate` / `ExecuteDelete` / `FromSql` anywhere in production targets
  `Orders` — I searched all of `Core.Domain`, `Core.AppServices`, `Infra.Database`, `Infra.Services`,
  `Infra.Clients`, `Config`, all five Web hosts and `Functions`; the hits are Users
  (`UserRepository.cs:164,186,206,222`), PromoCodes (`PromoCodeRepository.cs:43,61`),
  UserNotifications, RefreshTokens, MembershipBenefitUsage, GdprRequests and AdminUsers.
  `DataRetentionBackgroundService.CleanOrderCustomerPiiAsync` (`:136-171`) is entity-tracked and calls
  `Order.AnonymizeCustomerData` (`Order.cs:681-708`), which touches PII and never the status.
- **W6** — no `.sql` file under `sql-scripts/` mentions `CurrentStatus` at all (searched). The
  "idempotent backfill" the old comment promised genuinely does not exist. W6 is correct as written.
- **EF re-attach / detached `Update` / partial materialisation** — zero `Attach(`, zero
  `DbContext.Update(order)`, zero `Entry(order)` in production code. Every `.Update(` hit is a domain
  mutator method (`address.Update`, `user.Update`, `service.Update`, …), not an EF call. No
  `new Order(`, no `Activator.CreateInstance` against `Order`, no JSON deserialisation into `Order`.
  EF entity materialisation always populates mapped scalars, so a partially-loaded graph cannot yield
  a defaulted `CurrentStatus`.
- **The throw window the brief flagged** — `BookingPolicy.ExceedsMaxBookableSpan` throws at
  `OrderFactory.cs:158-165`, *before* `Add` at `:222`. And
  `src/Cleansia.Core.AppServices/Behaviors/UnitOfWorkPipelineBehavior.cs:20-30` commits only after
  `next()` returns **and** only on `BusinessResult { IsSuccess: true }` — an exception skips the commit
  entirely. No partially-built order can be persisted through the throw path. I also checked the
  subtler variant: `OrderService.Create` / `OrderPackage.Create`
  (`src/Cleansia.Core.Domain/Orders/OrderService.cs:14-20`) copy ids and references only and never add
  themselves to a back-collection on the **tracked** `Service` / `Package`, so EF's change detection
  has no navigation path from a tracked entity into the untracked `order`. The graph cannot be
  discovered and inserted behind the factory's back.
- **`default(OrderStatus) == New` observability inside the factory window** — between `:110` and
  `:221` the `order` is handed to `OrderService.Create` / `OrderPackage.Create` (`:139`, `:145`),
  `notificationProducer.NotifyAsync` (`:192-202`, given `order.Id` / `order.DisplayOrderNumber` only),
  and `vatCalculator.Calculate(order.TotalPrice, …)` (`:213`). **None reads `CurrentStatus`.**
  `OrderStatusTrack.Create` (`src/Cleansia.Core.Domain/Orders/OrderStatusTrack.cs:20-25`) does not read
  it either — there is no "previous status" capture — so the audit trail is unaffected by the default.
  I could not find a production caller that silently does the wrong thing with `New`; CH-W1's case is
  a test fixture, and it is a warning about the residual's *shape*, not a reachable counter-example.

**The two consequences the ADR flagged, since they shipped:**

- **The recompute inside `AddOrderStatus` stayed verbatim.** `Order.cs:451-456` is still
  `_orderStatusHistory.OrderByDescending(s => s.CreatedOn).ThenByDescending(s => s.Sequence).First().Status`,
  with the "recompute rather than blindly take the appended status" comment intact at `:450-452`. Only
  the assignment target changed, exactly as §3 required. It is **test-pinned in both directions**, so
  a future "simplification" to `CurrentStatus = orderStatusTrack.Status` fails CI rather than review:
  `OrderCurrentStatusPersistenceTests.Backdated_Append_Does_Not_Become_Current` (`:161-173`) and
  `Same_Timestamp_Transitions_Tiebreak_On_Sequence` (`:147-159`), plus
  `OrderSpecificationCurrentStatusTests.Tied_Timestamps_Resolve_By_Sequence_In_The_Filter` (`:93-112`).
  This is the part of the ADR I would defend hardest.
- **The six deletions were the right six.** All confirmed removed, none over-reached:
  `HasOverlappingOrderStatusTests` retains no NULL reference; `OrderCurrentStatusPersistenceTests` has
  five tests and no NULL case; `ColdPathCurrentStatusQueryTests` likewise;
  `TakeOrderOfferabilityGateTests` contains **no reflection at all** (no `GetField`, no
  `BindingFlags`) — the runtime-only failure §4.4 specifically warned the compiler would not catch was
  in fact caught. `OrderOfferabilityAgreementTests.Cases` (`:52-62`) is eight rows with no NULL row,
  and **every money-axis row survives** (`order-new-cash-oneoff`, `order-new-cash-recur`,
  `order-conf-cash-recur`, `order-conf-card-paid`, `order-conf-card-pend`) — §1's "the rest of
  `TC-AVAIL-EQUIV` stands, including the row per money term" holds, and the two-forms equivalence test
  at `:155-201` still pins SQL against C# over real Postgres.
  `OrderListProjectionEquivalenceTests` kept its Sequence-tie fixture and gave it real tracks
  (`:117-119`) instead of deleting the row — the right call (only the comment is stale, CH-W5).
- **The mechanical §8 items that did land:** `_currentStatus` → zero hits under `src/`;
  `HasField` / `UsePropertyAccessMode(Field)` / `IsRequired(false)` gone from
  `src/Cleansia.Infra.Database/EntityConfigurations/OrderEntityConfiguration.cs:102-103` (now a bare
  `.IsRequired()`); the index line `:109` unchanged; `20260723182623_Initial.cs:1046` is
  `nullable: false` and `:3068-3070` leaves `IX_Orders_CurrentStatus_CleaningDateTime` untouched;
  `OrderAvailability.IsOfferable`'s first parameter is non-nullable (`OrderAvailability.cs:56`);
  `TakeOrder.OfferabilityProbe.CurrentStatus` dropped its `?` (`TakeOrder.cs:125`) while the `probe?.`
  lifts at `:96` / `:102` still compile as §4.2 predicted; `OrderRepository.cs:85-91`'s IS-NULL claim
  is gone; the `currentRank < 0` guard survives at `AdminOverrideOrderStatus.cs:118` with its ADR-0037
  D5 rationale intact; and `DataRetentionBackgroundService.cs:149`'s
  `OrderStatusHistory.Any(h => h.Status == Completed)` — §3's explicitly flagged trap — was correctly
  **left alone**.

---

## Bottom line

**No blocker. Nothing to revert.** The write-time guarantee holds on every path I could find, the
recompute survived intact and test-pinned, and the six deletions were correct.

Ordered by urgency:

1. **CH-W3 + CH-P3 — before the next deploy, not before merge.** Prove the regenerated `Initial` was
   *applied* (not just committed) on every live environment. `MigrationService` will report success on
   a database that silently ignored the edit, and the deleted correlated subquery then fails **open**
   on the booking write gate. Green tests cannot see this.
2. **CH-W1 + CH-W2 — before `accepted`.** Correct §3's "benign default" to name the fail-open
   direction, file the deferred `New`-in-`Create` ticket, and land the `consistency.md` edit +
   enforcement ticket §8 item 8 already promised — including fixing `consistency.md:310`, which today
   states the opposite of the shipped code.
3. **CH-P1 + CH-P2 + CH-P4 + CH-P6 — before `accepted`, editorial but load-bearing.** §5's "certain"
   half needs to say what is actually certain: one site had the `OR`, the sargability comes from the
   C# deletion rather than the constraint, and the offerability path keeps its own `OR`.
4. **CH-P5 — a ticket referenced from the Verdict.** The `EXPLAIN` harness already exists; the
   obligation two ADRs have deferred is one file, and it would pin §5 at the same time.
5. **CH-W4, CH-W5, line drift — cheap, do them in the same pass.**
