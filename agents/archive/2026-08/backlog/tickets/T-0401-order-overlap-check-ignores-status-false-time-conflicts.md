---
id: T-0401
title: "Backend — HasOverlappingOrderAsync counts assigned orders regardless of status, so stale/blocked orders cause false order.time_conflict rejections on TakeOrder"
status: done
size: S
owner: backend
created: 2026-07-12
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, db]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix3 round-9 gate (empirical — every take but one was rejected with order.time_conflict on the dev backend because the test account carried a stale un-completable order)
---

> **Found empirically by the round-9 gate.** While taking an order on the dev backend to render the Active-tab
> card, every take but one was rejected with `order.time_conflict`. Cause:
> `OrderRepository.HasOverlappingOrderAsync` (`src/Cleansia.Infra.Database/Repositories/OrderRepository.cs:216`)
> counts **ALL** orders assigned to the employee in the time window **regardless of status** — the test
> account's stale in-progress order (whose completion is photo-gated and can't be finished) plus history rows
> blocked every overlapping slot. A cleaner with any lingering assigned order — including states that can never
> become active work (e.g. long-past, cancelled-adjacent, or perpetually blocked ones) — gets false conflicts.

## Acceptance criteria
- [ ] **AC1** — the overlap check counts only orders in statuses that represent REAL future/active work
  (decide the exact set with the state machine: New/Pending/Confirmed/InProgress — and evaluate whether
  long-past `InProgress` rows should block future slots at all, or whether the check should also bound by
  date). Cancelled/Completed orders never conflict.
- [ ] **AC2** — unit/integration tests pin: an overlapping Completed order does not conflict; an overlapping
  Cancelled order does not conflict; an overlapping Confirmed order DOES; the boundary semantics unchanged.
- [ ] **AC3** — `dotnet test` green; no wire/DTO change (validator-internal predicate only).

## Status log
- 2026-07-12 — filed `proposed` from the round-9 gate's empirical finding on the dev backend.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped `a415a3d2`: non-terminal assigned orders block; fail-closed null fallback.
