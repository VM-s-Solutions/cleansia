---
id: T-0841
title: Give every system job that continues past a failed commit one scope per item, SendRecurringOrderReminders and RedrivePendingRefunds first, and make CancelUnfilledOrders re-read the order it cancels
size: L
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0844]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

**The rule.** T-0844's job-units decision record is ADR-0072, D2. It states the rule:
- a job whose items must fail independently gives each item its own scope;
- a loop never continues past a failed commit, or past staging that a later commit in the same scope will save, with or without `Rollback()`.

**Why it matters.** A failed `CommitAsync` leaves the tracked entries as they were (`CleansiaDbContext.cs:84-120`). So a failed item's leftovers ride the next item's commit, and a fault that repeats fails every later commit in the run (BE-13).

**Where this ticket comes from.**
- T-0844 fixes the recurring auto-cancel and lists the remaining cases in a deviating-forms entry in `consistency.md`. This ticket retires that entry.
- The 2026-10-12 design ruling on BE-11 to BE-14 reported these as items 3 and 4, and recommended filing this ticket. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_be-lead.json`.

**The deviations** were read from the tree by the ruling, with a search for `catch (Exception` and `Rollback()` under `src/Cleansia.Core.AppServices`. Lines are as of `76e1638a3`.
- **A commit inside the try, or `Rollback()` and continue:**
  - `SendRecurringOrderReminders.cs:62-92`;
  - `RedrivePendingRefunds.cs:120-218`, with the catch at `:208-218`. This job moves money;
  - `FiscalRetryService.cs:40-112`;
  - `PayPeriodBackgroundService.cs:255-279`, with `:455-476`;
  - `SendMembershipLifecycleNotifications.cs:67-89` and `:105-127`;
  - `NewJobsDigestService.cs:110-244`, which commits at `:376`;
  - `DataRetentionBackgroundService.cs:93-105`.
- **A catch over staging only, then a group commit.** These are milder:
  - `SendPreCleaningReminders.cs:87-112`;
  - `NotifyLapsedPreferredOffers.cs:88-102`;
  - `SendCleanerJobReminders.cs:175-201`;
  - `SendTomorrowJobDigest.cs:138-153`;
  - `PayPeriodBackgroundService.cs:156-172`.

**SendRecurringOrderReminders goes first.** Once T-0844's `PaymentStatus` token lands, a payment that arrives during this job's run fails every later reminder in that run. This is rare: the run takes seconds, at 02:30 UTC.

**CancelUnfilledOrders' re-check sees nothing new.**
- Its comment (`CancelUnfilledOrders.cs:131-133`) says the in-memory re-check catches a seat filled after the read. It cannot, because tracked entities are never refreshed.
- The `CurrentStatus` token catches only a take that moves New to Confirmed. A take onto a Confirmed order with no crew appends no status track (`TakeOrder.cs:406-412`), so nothing catches it.
- So the sweep can cancel a job a cleaner has just taken. This is rare, because a Confirmed order with no crew is itself rare (ADR-0067).

**If not built:** in each listed job, a failing item holds back every later item in the same run until the next run. For `RedrivePendingRefunds`, the next run is an hour later.

## Doing (proposed, for Mike)

- **One job per change.** Each job gets its own isolation RED: an item that throws leaves the others untouched, and a fault that repeats holds back nobody.
- **The order:**
  1. `SendRecurringOrderReminders`, with or after T-0840, which edits the same file;
  2. `RedrivePendingRefunds`;
  3. the rest of the first group;
  4. the staging-only group.
- **The shape, chosen per job.** Each job takes one of T-0844's two shapes:
  - the dispatcher-and-unit shape of `MaterializeRecurringBookings` and `AutoCancelRecurringOccurrence`. Each item gets one DI scope. The unit re-reads and re-checks its row, takes its tenant from that row, and has no catch and no `CommitAsync`;
  - no catch at all, where failing the tick is the right outcome.

  `DataRetentionBackgroundService` keeps its companies-in tenancy shape, which CLAUDE.md names as the reference.
- **`CancelUnfilledOrders`.**
  - Each order is re-read fresh before it is cancelled, in its own scope or with a reload.
  - The comment at `:131-133` is rewritten to say what is true.
  - A PostgreSQL RED with two connections: a take onto a crewless Confirmed order inside the sweep's window. Today the sweep cancels it.
- **The deviating-forms entry** drops each job as it lands, and is deleted when the last one does.
- **Split before it goes ready**, into one ticket per job or per group. As filed, it is L.

## NOT doing

- The stale-checkout sweep keeps one commit per company. It is the reference shape, and its two-operator tests pin it.
- No attempt caps. The job-units decision record accepts their absence.
- The reminder sweep's window and status term (T-0840).

## Acceptance criteria (done looks like)

- [ ] **AC1** — Each listed job runs each item in its own scope, or fails the tick. Each job's isolation test goes RED then GREEN.
- [ ] **AC2** — `CancelUnfilledOrders` does not cancel an order whose seat was taken after its read. The PostgreSQL test goes RED then GREEN.
- [ ] **AC3** — The deviating-forms entry is gone, and the full suites pass.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 3 and 4, and its recommendation under BE-13. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work, and when.

## Review

Pending.
