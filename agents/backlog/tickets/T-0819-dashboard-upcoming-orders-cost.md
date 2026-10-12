---
id: T-0819
title: Find out why the dashboard's upcoming-orders read is ten times slower, and fix it if the query is at fault
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
---

## Context

Tonight's iOS single dashboard update waits for `GetUpcomingOrders`: about 1.4 s against about 0.2 s for the other reads on the render fixture [BE-17, IOS-07]. Mike's ruling on the iOS side is to switch to progressive publication only if the wait is noticeable on DEV, which needs his own check. If the read itself is slow, fixing it helps both apps whatever he finds.

## Doing

- `EXPLAIN (ANALYZE, BUFFERS)` and n ≥ 5 warm timings of the three dashboard statements for the render cleaner on the local fixture.
- If the cause is query-level and needs no schema change (over-fetch, includes, a missing filter), fix it tests-first and re-measure. Otherwise write down the cause and the costed options.

## NOT doing

- No index, migration or DTO change without a separate decision.
- No iOS progressive change: that stays with Mike's DEV check (D4).

## Acceptance criteria (done looks like)

- [x] **AC1** — The cause is recorded with plans and timings.
- [x] **AC2** — Either a measured fix with green suites, or a costed option list for Mike.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Investigated by the backend lane on the local render fixture, in read-only sessions only. The fixture has 100,000 orders. The render cleaner has 200 assignments, in a company of 80,000 orders.
  - **Cause.** `OrderSpecification`'s employee filter, `AssignedEmployees.Select(x => x.EmployeeId).Contains(id)`, becomes `@id IN (correlated subquery)`.
    - PostgreSQL cannot make that a semi-join.
    - So the count statement and the page-id statement each scan all 80,000 company orders through `IX_Orders_TenantId`, running the subquery once per row: 304,290 of 308,720 buffer hits.
    - The plan's cost also switches JIT on, at about 185–295 ms per statement (up to 499 ms in the one noisy run).
  - **Fix, at query level only.** `AssignedEmployees.Any(ae => ae.EmployeeId == id)` becomes an EXISTS. It is planned as a hash aggregate over `IX_OrderEmployees_EmployeeId` plus primary-key lookups: 1,764 buffer hits, 1.5–6 ms, and no JIT.
  - **Measured**, n = 7 after 2 warm-ups, in ABBA order:
    - sequential median of the upcoming read: from 970 ms to 18–19 ms. The pass's other before-leg, 2,653 ms, was machine-noisy; an earlier unpaired run read 940 ms;
    - concurrent: from 1,037–1,061 ms to 24 ms in the clean run;
    - the page is identical: total 200, and the same ten ids.
  - Receipt (off-tree, with the plans and the harness): `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/backend/t0819/t0819-measurement-receipt.json`, sha256 `35727fb8…`.
  - RED: the new `The_Employee_Filter_Is_An_Exists_On_That_Employee` fails on the old predicate ("Match found … @EmployeeId IN (SELECT").
  - GREEN: it passes with the fix, and the focused suites pass 104/104.
- 2026-10-12 — Committed as `64f40fa`, which builds on its own.

## Review

- **Correctness: approve.**
- **Security: S8 pass.**
  - The emitted SQL keeps the Orders tenant term.
  - `OrderEmployee` has no filter in either form.
  - Counts and id sets are identical.
- **Gate 7:** no CHANGELOG entry. The rows are the same, only faster, and at today's scale the speed-up is not noticeable.
- **Options not taken, for Mike:**
  - a PostgreSQL plan pin for the "mine" read: about 1–2 h, and optional;
  - a server JIT setting: an infrastructure decision, not needed after this fix.

  The iOS progressive dashboard stays his DEV check (D4).
- **New finding, reported.** The apps' "upcoming" request had no date filter, and the native clients' sort parameter does not bind on the server. So the page was not the cleaner's next jobs. T-0831, in the native PR, adds the from-today filter. The sort encoding is reported there for a ticket of its own.
