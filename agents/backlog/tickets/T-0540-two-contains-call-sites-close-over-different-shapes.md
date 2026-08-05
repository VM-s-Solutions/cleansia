---
id: T-0540
title: Two status `Contains` call sites close over different shapes and may not emit the same SQL — nothing pins it
status: done
size: S
owner: db
created: 2026-08-04
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: [0037, 0039, 0040]
layers: [db, backend]
security_touching: false
manual_steps: []
sprint: 15
source: raised while `7e1cf7f5` made `Order.CurrentStatus` NOT NULL and collapsed the overlap
  predicate's fail-closed disjunct into "one sargable `Contains` on the indexed column", and sharpened
  by ADR-0040's challenger (`44d1b64d` CH-P3). Filed by the PM in the sprint-15 reconciliation.
---

## Context

There are exactly **two** production call sites that test an order's status with `Contains`, and they
close over **different shapes**:

| site | closes over | likely SQL |
|---|---|---|
| `Core.Domain/Specifications/OrderSpecification.cs:129` — `OrderStatuses.Contains(x.CurrentStatus)` | an **instance property**, `IEnumerable<OrderStatus>? OrderStatuses` (`:23`) — a runtime value | a parameterized `= ANY(@p)` |
| `Infra.Database/Repositories/OrderRepository.cs:329` — `SlotBlockingStatuses.Contains(o.CurrentStatus)` | a **`private static readonly OrderStatus[]`** (`:261-269`) — a compile-time constant | EF may **inline** the constants as an `IN (…)` list |

Both were written to seek on the leading column of `IX_Orders_CurrentStatus_CleaningDateTime`, and both
carry a comment saying so — `OrderSpecification.cs:124-128` says *"this is a bare IN — no null conjunct
pushing the term inside an OR, which is what lets the planner seek"*, and `OrderRepository.cs:299-302`
says the two selective terms *"then sit together on IX_Orders_CurrentStatus_CleaningDateTime"*.

**Two comments asserting a plan property that nothing verifies is the exact defect class this sprint has
been closing all week.** Three false "mirrors X" comments, a backfill script that never existed, a
mitigation that lived only in a comment — each was a claim in prose that no test could contradict. These
two are the same shape, on the **booking write gate** and the **partner board's only authoritative
floor**.

**ADR-0040's challenger made it concrete rather than theoretical.** CH-P3: for a NULL row the new
`= ANY(...)` yields NULL and **excludes** it, where the old second arm consulted history and could
include it — so **on a drifted schema the overlap check FAILS OPEN and permits a double booking**, and
neither the overlap predicate nor the busy-set query materialises an `Order`, so a drifted schema raises
no error. **It would be silent.** That risk is retired by the owner's database drop, not by the code —
which is precisely why the emitted SQL deserves a pin rather than a comment.

**CH-P5 removed the usual excuse.** The EXPLAIN obligation two ADRs have now deferred *"is one file, not
a research task: the repo already contains a complete working template with its own container, a skewed
seed, ANALYZE and a no-Seq-Scan assertion."* The challenger could not execute it in its sandbox and
therefore **did not claim to discharge it** — only refuted its implied cost.

## Acceptance criteria

- [x] **AC1 — the two emitted statements are captured, not reasoned about.** Given each call site, When
      its query is executed against **real PostgreSQL** (Testcontainers, the same harness the existing
      template uses), Then the SQL text EF emits for each is captured and recorded in this ticket.
      **Reading the LINQ and asserting what EF "will" do is the thing this ticket exists to replace.**
- [x] **AC2 — the plan is asserted, on a skewed seed, after ANALYZE.** Given each query, When `EXPLAIN`
      runs against a seed skewed enough for the planner to have a choice, Then the assertion is
      **no Seq Scan on `Orders`** and the index used is `IX_Orders_CurrentStatus_CleaningDateTime`.
      A plan assertion on an empty or uniform table proves nothing.
- [x] **AC3 — the assertion can fail.** Given the pin, When the status term is mutated (widen the set to
      every enum member, or push the term inside an `OR`), Then the test goes **red** and names which
      query regressed. **Evidence: the mutation, run, then reverted.**
- [x] **AC4 — if the two shapes emit materially different SQL, the difference is either eliminated or
      documented at both sites.** Given AC1's two statements, When they differ in a way that changes the
      plan, Then either both sites are brought to one shape, **or** each comment is corrected to state
      what it actually emits and why the difference is acceptable. **The prose must end up true either
      way** — that is the point of the ticket, not a preference for one shape.
- [x] **AC5 — no behavioural change.** Given this is a pinning ticket, When it lands, Then no predicate's
      result set changes. If making the shapes agree would change a result set, **stop and file** — that
      is a behaviour change and needs its own ticket.

## Evidence

**Verdict: a risk, not a defect.** The two shapes emit **different SQL text** and the **same plan
node**. The prose was true; nothing kept it true. The deliverable is therefore the pin plus the
correction of one factually wrong word, not a fix.

Pin: `src/Cleansia.IntegrationTests/Features/Orders/OrderStatusSetPredicatePlanTests.cs` — own
Testcontainers Postgres, `EnsureCreated` from the current model, FKs on `Orders`/`OrderEmployees`
dropped, 32 008 orders skewed to terminal statuses over two years, `ANALYZE`. Each call site is driven
through a **production entry point**; a `DbCommandInterceptor` re-runs `"EXPLAIN " + CommandText` on the
same connection, transaction and parameter values, so the plan asserted is the plan the query gets.

### AC1 — the statements EF actually emits (captured, not reasoned about)

`OrderSpecification.cs:129` (instance `IEnumerable<OrderStatus>`), via
`DashboardSpecifications.CreateCompletedOrdersSpec` → `OrderRepository.GetCountAsync`:

```sql
SELECT count(*)::int FROM "Orders" AS o
WHERE (@ef_filter__p4 OR (@ef_filter__p2 AND o."TenantId" IS NULL) OR o."TenantId" IS NULL)
  AND @EmployeeId IN (SELECT o0."EmployeeId" FROM "OrderEmployees" AS o0 WHERE o."Id" = o0."OrderId")
  AND o."CleaningDateTime" >= @CleaningDateFrom_Value
  AND o."CleaningDateTime" <= @CleaningDateTo_Value
  AND o."CurrentStatus" = ANY (@OrderStatuses)          -- @OrderStatuses :: System.Int32[] = {5}
```

`OrderRepository.cs:329` (`private static readonly OrderStatus[]`), via `HasOverlappingOrderAsync`:

```sql
SELECT EXISTS (
    SELECT 1 FROM "Orders" AS o
    WHERE (…tenant filter…)
      AND o."CleaningDateTime" >= @scanFloor AND o."CleaningDateTime" < @windowEndUtc
      AND o."CleaningDateTime" + CAST(o."EstimatedTime"::double precision::text || ' mins' AS interval)
          > @windowStartUtc
      AND o."CurrentStatus" IN (0, 1, 2, 3, 4)          -- inlined, no parameter
      AND EXISTS (SELECT 1 FROM "OrderEmployees" AS o0
                  WHERE o."Id" = o0."OrderId" AND o0."EmployeeId" = @employeeId))
```

…and via `GetBusyEmployeeIdsInWindowAsync`, same `IN (0, 1, 2, 3, 4)` term over a `DISTINCT` join.

**So the ticket's prediction is confirmed: EF parameterises the runtime value and folds the
`static readonly` array to constants.** The difference is real and is in the SQL text.

### AC2 — the plans (skewed seed, after ANALYZE)

PostgreSQL parses `IN (const-list)` into the **same `ScalarArrayOpExpr`** as `= ANY (array)`, so both
shapes arrive at the planner identically. All three queries put the status term in the **index
condition** of `IX_Orders_CurrentStatus_CleaningDateTime`; none seq-scans `Orders`:

| query | scan node | index cond |
|---|---|---|
| `CreateCompletedOrdersSpec` | `Bitmap Index Scan on "IX_Orders_CurrentStatus_CleaningDateTime"` | `("CurrentStatus" = ANY ('{5}'::integer[])) AND ("CleaningDateTime" >= …) AND (… <= …)` |
| `HasOverlappingOrderAsync` | `Index Scan using "IX_Orders_CurrentStatus_CleaningDateTime"` | `("CurrentStatus" = ANY ('{0,1,2,3,4}'::integer[])) AND ("CleaningDateTime" >= …) AND (… < …)` |
| `GetBusyEmployeeIdsInWindowAsync` | `Bitmap Index Scan on "IX_Orders_CurrentStatus_CleaningDateTime"` | same as above |

Two seed properties are load-bearing, both learned by measurement:
- The **status band must stay a minority** (here 6.3% live) or a seq scan is the *correct* plan and the
  assertion is vacuous — pinned by its own test.
- The probe cleaner needs a **long assignment history**. With only the 8 in-window rows the planner
  drives the overlap query off `IX_OrderEmployees_EmployeeId` and `PK_Orders` — still no seq scan, but
  the scan-floor rationale `LiveCommitmentsInWindow` documents is never exercised.

Npgsql auto-prepare is **off** across the repo (no `Max Auto Prepare` anywhere), so the parameterised
arm is planned with actual values on every execution — there is no generic-plan hazard that would make
the textual difference material.

### AC3 — mutation table (mutated → run → restored byte-exact, verified by SHA-256)

| # | mutation | result | failing tests |
|---|---|---|---|
| — | baseline | **8 passed, 0 failed** (exit 0) | — |
| M1 | `SlotBlockingStatuses` widened with `Completed`, `Cancelled` (AC3's "every enum member") | **RED** — 2 failed, 6 passed (exit 1) | `TheLiveCommitmentStatusSetReachesPostgresExactly`, `TheInstancePropertyShapeEmits…InlinedInList` |
| M2 | `OrderSpecification.cs:129` status term pushed inside an `OR` with the ADR-0040 latest-history disjunct | **RED** — 2 failed, 6 passed (exit 1) | `EveryStatusSetPredicateIsAnIndexConditionOnTheCurrentStatusIndex`, `BothContainsShapesNormaliseToTheSameScalarArrayOperator` |
| M3 | `SlotBlockingStatuses` closed over via a local, so both sites emit `= ANY (@p)` | **RED** — 1 failed, 7 passed (exit 1) | `TheInstancePropertyShapeEmits…InlinedInList` |
| — | restored (both files SHA-256-identical to pre-mutation) | **8 passed, 0 failed** (exit 0) | — |

M1 message: `LiveCommitmentsInWindow → HasOverlappingOrderAsync: the live-commitment status set changed.
expected [0,1,2,3,4] but PostgreSQL received [0,1,2,3,4,5,6].`

**M2 is the finding that changes how this class of test should be written.** Under the `OR` the planner
**still used `IX_Orders_CurrentStatus_CleaningDateTime`** and merely demoted the status term out of the
index condition into a residual filter:

```
Index Cond: (("CleaningDateTime" >= …) AND ("CleaningDateTime" <= …))   ← "CurrentStatus" gone
```

A "no Seq Scan" assertion — AC2's literal wording, and what the existing membership template does — is
**green** on that mutation. The `Index Cond` assertion is what has teeth. Both are kept.

**M3 is the direct answer to the ticket's question.** Bringing the static-array site onto the
parameterised shape left the other **7 tests green** — same index, same index condition, same
`ScalarArrayOpExpr`. That is positive evidence that the textual difference is not a plan difference,
rather than an argument that it should not be.

### AC4 / AC5 — the prose, and nothing else, changed

The difference cannot be eliminated in the good direction (site A's set is a runtime value and can
never inline; forcing site B to parameterise would *lose* the planner's compile-time constants for no
gain), so this takes AC4's second branch — corrected at both sites, each naming the pin:

- `OrderSpecification.cs` said *"this is a bare **IN**"*. It emits `= ANY (@p)`. Corrected, and it now
  records M2's finding: an `OR` here keeps the index and demotes the term.
- `OrderRepository.cs` made no false claim but did not say the static array inlines. It now does, with
  the `ScalarArrayOpExpr` normalisation as the reason the difference is acceptable.

**AC5:** the diff on both production files is **comment-only** — not one executable token changed
(`git diff` is five comment lines in one file, four in the other). No result set can have moved.

## Out of scope

- Making `CurrentStatus` NOT NULL — shipped in `7e1cf7f5`.
- The `= ANY(...)` NULL-exclusion hazard itself (ADR-0040 CH-P3). It is retired by the **database drop**,
  which is an owner step. This ticket pins the plan; it does not defend a drifted schema.
- Adding an index. If AC2 shows the index is not chosen, record it and file — do not invent an index
  under a pinning ticket.
- The other `Contains` sites in `OrderRepository` (`:301` `employeeIds`, `:440` `candidateIds`). They are
  id sets, not status sets, and the leading-column claim is not made about them.

## Implementation notes

**Read first:** ADR-0040's challenge record (`44d1b64d`) for CH-P3 and CH-P5, and ADR-0037 §4.1/§5 —
which, per CH-P1, **contradict each other** on where the disjunct was (§4.1 records the specification's
old term as a conjunction of two sargable quals; §5 says it sat inside an `OR` and was not an index qual
at all — **only the overlap predicate had the `OR`**). Do not take either as the premise; read the code.

The existing EXPLAIN template CH-P5 names is the model — reuse it rather than building a harness.

**Archetype:** `agents/knowledge/patterns-backend.md` (specifications + repository query shapes) and
`consistency.md` (a claim in a comment must be pinned by something that can fail).

**No-decision note:** this ticket adds evidence for an already-accepted design. It makes no new decision
and must not change behaviour (AC5). No panel.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Both call sites re-verified at
  HEAD (`OrderSpecification.cs:129` over an instance `IEnumerable`, `OrderRepository.cs:329` over a
  `static readonly` array). Passes DoR: AC observable, `S`, no dependencies, no owner-only steps.
- 2026-08-05 — implemented by db. Gate 0: **risk, not defect** — measured against real Postgres, the two
  shapes emit different SQL (`= ANY (@p)` vs `IN (0, 1, 2, 3, 4)`) and one plan node; both are index
  conditions on `IX_Orders_CurrentStatus_CleaningDateTime`. Pinned by
  `OrderStatusSetPredicatePlanTests` (8 tests), mutation-proved M1/M2/M3 with byte-exact restore.
  Prose corrected at both sites; one word (`IN`) was factually wrong. Comment-only production diff.
  Harvested into `consistency.md` (EXPLAIN the captured statement; assert the `Index Cond`, not
  "no Seq Scan"). **No migration, no owner step.** Suites: integration 140/140 exit 0, unit 3035/3037
  exit 1 — the 2 failures are `ImageFileValidatorTests`, another agent's untracked in-flight lane,
  untouched here.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
