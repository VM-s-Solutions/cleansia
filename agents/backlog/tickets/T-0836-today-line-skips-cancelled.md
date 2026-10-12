---
id: T-0836
title: Don't count a job cancelled today on the partner dashboard's today line
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0831]
blocks: []
stories: []
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Found on 2026-10-12. It existed before, and T-0831 now makes it reachable for every cleaner.
- `Order.Cancel` (`Order.cs:1091-1103`) keeps `AssignedEmployees`, and `CustomerOrderCancellation.cs:130` notifies the cleaners still assigned.
- Android's `todaysJobCount` (`DashboardScreen.kt:1137-1141`) and iOS's `DashboardData.todaysJobsCount` count every status.

So a job the customer cancelled today still reads as *1 job today*, while the hero skips it. T-0831's NOT doing considered only Completed jobs.

## Doing (proposed, for Mike)

- Both apps' today count skips Cancelled. Whether a job cancelled today should count at all is Mike's product call. Recommended: it should not.
- The features page says so.

## NOT doing

- No backend change.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A job cancelled today does not count on either app. The tests go RED, then GREEN.
- [ ] **AC2** — The full suites pass.

## Work record

- 2026-10-12 — Filed from the correctness re-review of the native PR (T-0831). Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
