---
id: T-0819
title: Find out why the dashboard's upcoming-orders read is ten times slower, and fix it if the query is at fault
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-11
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

- [ ] **AC1** — The cause is recorded with plans and timings.
- [ ] **AC2** — Either a measured fix with green suites, or a costed option list for Mike.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
