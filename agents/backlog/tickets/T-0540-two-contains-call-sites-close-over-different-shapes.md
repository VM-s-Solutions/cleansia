---
id: T-0540
title: Two status `Contains` call sites close over different shapes and may not emit the same SQL — nothing pins it
status: ready
size: S
owner: db
created: 2026-08-04
updated: 2026-08-04
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

- [ ] **AC1 — the two emitted statements are captured, not reasoned about.** Given each call site, When
      its query is executed against **real PostgreSQL** (Testcontainers, the same harness the existing
      template uses), Then the SQL text EF emits for each is captured and recorded in this ticket.
      **Reading the LINQ and asserting what EF "will" do is the thing this ticket exists to replace.**
- [ ] **AC2 — the plan is asserted, on a skewed seed, after ANALYZE.** Given each query, When `EXPLAIN`
      runs against a seed skewed enough for the planner to have a choice, Then the assertion is
      **no Seq Scan on `Orders`** and the index used is `IX_Orders_CurrentStatus_CleaningDateTime`.
      A plan assertion on an empty or uniform table proves nothing.
- [ ] **AC3 — the assertion can fail.** Given the pin, When the status term is mutated (widen the set to
      every enum member, or push the term inside an `OR`), Then the test goes **red** and names which
      query regressed. **Evidence: the mutation, run, then reverted.**
- [ ] **AC4 — if the two shapes emit materially different SQL, the difference is either eliminated or
      documented at both sites.** Given AC1's two statements, When they differ in a way that changes the
      plan, Then either both sites are brought to one shape, **or** each comment is corrected to state
      what it actually emits and why the difference is acceptable. **The prose must end up true either
      way** — that is the point of the ticket, not a preference for one shape.
- [ ] **AC5 — no behavioural change.** Given this is a pinning ticket, When it lands, Then no predicate's
      result set changes. If making the shapes agree would change a result set, **stop and file** — that
      is a behaviour change and needs its own ticket.

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

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
