---
id: T-0808
title: Wave D — one commitment read per cleaner in the new-jobs digest, one recipient read per company in the stale-checkout sweep
size: M
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0807]
blocks: []
stories: []
adrs: [ADR-0039]
layers: [backend, docs]
security_touching: true
---

## Context

Wave D of the 2026-10-07 technical audit (`agents/AUDIT-2026-10-07.md`, B005 and B006). Mike authorized it on 2026-10-10, with recommended routine choices, as the next implementation scope after Wave C and its findings track (T-0807).

A read-only ground truth checked both findings against the tree before any code. Mike was away when the choices were made. Each choice is the ground truth's recommended or ADR-literal option; all are listed here and each is reversible.

- **B005.** `NewJobsDigestService` asked the database whether a cleaner already had an overlapping commitment once per surviving candidate job. On the audit's 10-cleaner / 100-job fixture that is 660 of the run's 724 SQL commands.
- **B006.** `CleanupStalePendingOrders` resolved each cancelled order's recipient company with its own read, through `NotificationProducer.NotifyAsync`. On the audit's 1,000-order sweep that is 1,000 of the run's 1,010 SQL commands.

## Doing

- **B005.** One tenant-ignoring commitment-window read per cleaner with surviving candidates, from the earliest candidate start minus `MaxOrderSpanHours` to the latest candidate end.
  - Each candidate is then judged in memory with the exact terms of the singular predicate: the half-open overlap, each candidate's own inclusive floor, and the same blocking statuses, which stay in SQL.
  - The pure rule is `Order.OccupiesWindow`, next to `MaxOrderSpanHours`, as ADR-0039 D3.3 asks.
  - A real-PostgreSQL agreement test pins the batch against `HasOverlappingOrderIgnoringTenantAsync` on identical rows.
  - No surviving candidate means no commitment SQL.
- **B006, cause 1 only.** Before the per-order work, each tenant group notifies through `NotificationProducer.NotifyEachAsync`. It makes one tenant-ignoring read of the distinct recipients' companies, then records every item exactly as `NotifyAsync` does, in input order.
  - An absent or company-less recipient still logs and is skipped.
  - Feed collapse, the push key, the envelope and outbox dedup are unchanged.
  - An order's or operator's hint never decides ownership.
  - Every `UserId` must be server-read.
- **Docs the change makes false:**
  - `docs/domain/roles/preferred-cleaner-hold-resolver.md`;
  - the dispatch decision doc, `agents/architecture/decisions/preferred-cleaner-dispatch.md`;
  - the notification-seam entry in `agents/knowledge/patterns-backend.md`, ratified by the architect.

## NOT doing

- **B006 cause 2, bounded pages, is not built.** Under the agreed constraints (one commit per tenant, no per-page commit, no `Tracker.Clear`, no new transaction), paging cannot lower peak tracked memory; it only adds reads. The options that would really bound the run are Mike's to choose:
  - (a) a per-run `OrderBy(CreatedOn, Id).Take(BatchSize)`, the `NotifyLapsedPreferredOffers` / `ExpireStaleCredit` idiom, where a backlog drains over several 15-minute ticks;
  - (b) detaching after each tenant commit;
  - (c) per-page commits.

  Still unbounded:
  - the change tracker across the whole run;
  - the O(N) candidate population;
  - `OutboxPendingDispatch._seen`.

  Credit return and cancellation are still not atomic: `TryReturnAsync` commits on its own.
- **`HasOverlappingOrderIgnoringTenantAsync` stays, now with no production caller.** Deleting it means re-pointing the S8 worked example (`docs/architecture/security-rules.md`) and its tenancy pins. That is an owner decision; security asks that it be decided before go-live.
- No schema, index, migration, DTO, NSwag or new framework. Not B009, not B010 (closure only), not Wave E/F.

## Acceptance criteria

- [x] **AC1** — RED→GREEN.
  - Call counts show one commitment read per cleaner with survivors, and zero when none survive, where there was one per candidate.
  - Cleanup groups make one recipient read per tenant group, where there was one per order.
  - The existing digest, overlap, busy-set, cleanup, credit-return, producer and tenancy suites stay green.
- [x] **AC2** — The PostgreSQL agreement test covers:
  - touching boundaries;
  - the inclusive scan floor;
  - every status;
  - cross-company commitments;
  - shorter and longer candidates;
  - a malformed long row that lowers the batch floor without blocking a later candidate.
- [x] **AC3** — Fresh n=5 before/after measurement on the restored synthetic fixture, interleaved, with a fresh restore and seed before every run. SQL count, elapsed time, allocation and exact outcome counts are reported as median and nearest-rank p90.
- [x] **AC4** — The Release backend build and the unit, integration and host suites pass on the final head; the repository checkers and the docs build pass.
- [x] **AC5** — Independent correctness, security, optimizer and architect reviews pass.
- [ ] **AC6** — Exact-head CI passes on every applicable workflow, and a normal SHA-guarded merge follows.

## Out of scope — reported, not fixed

- **Pre-existing false comments in `CleanupStalePendingOrders`.**
  - `:65-67` says the feed row inherits the group's override; it carries the recipient's company.
  - `:141-144` says the credit ledger row is stamped by the group commit; `TryReturnAsync` commits on its own. It now sits below the new, correct comment saying so.

  This is the first follow-up to schedule.
- `IOrderRepository.cs:173-174` calls `GetBusyEmployeeIdsInWindowAsync` tenant-scoped with no ignoring sibling. It reads ignoring tenancy, an unrecorded departure from ADR-0039 D3.2. The same claim sits in the dispatch decision doc's diagram.
- `PushSubjectNamesTheEventTests` sees only subjects passed positionally to `NotifyAsync(`. A subject inside a `NotifyEachAsync` item is not checked. That is harmless for the one allowlisted caller; it is noted in `patterns-backend.md`.
- `AutoCancelStaleRecurringOrders` and `CancelUnfilledOrders` still read each recipient's company per order.
- The `PaymentStatus` lost-update window in the stale sweep: `PaymentStatus` is not a concurrency token. It is pre-existing, and the window is narrowed, not widened.
- Doc drift:
  - `HasOverlappingOrderStatusTests`' class doc;
  - the stale `NewJobsDigestService.cs:155-158` citation in the hold resolver's role page;
  - "every freshness source is upper-bounded" (`docs/flows/offerability-and-take.md:150`), where the history source is not;
  - ADR line citations shifted by the producer refactor.
- Optional catalog harvest: an "one entity, many windows" bullet beside the "Ask N candidates ONE question" section.
- Environment-sensitive tests:
  - `CollectedFeeSharePayTests` fails 3 cases on a decimal-comma machine culture (see T-0807);
  - `BootDatabaseIoTests` timed out once under full-suite load.

## Work record

- 2026-10-10 — The ground truth was read-only. The design was chosen per cause and implemented tests-first, with four independent reviews.
- 2026-10-11 — The D branch, which had no commits yet, was fast-forwarded to master `64dfed7` (T-0807); nothing was rebased. T-0807's closure is its first commit. Commits:
  - `7bfe872` B005;
  - `026bf52` B006.

  The coordinator applied the reviews' must-land notes:
  - the dispatch decision doc and the seam entry, written by the architect;
  - one stale cost sentence;
  - the server-derived-id clause on `NotifyEachAsync`.

  Then the before/after job measurement ran.

## Review

Evidence lives off-tree under `/Users/michael/.codex/scratchpads/cleansia-wave-d-2026-10-10/`. The report is `agents/WAVE-D-2026-10-11.md`.

- **AC1 / AC2.**
  - B005: the characterization pins passed on the old code (78/78). With stubs, 25 tests were RED, including "single-window probes expected 0, actual 12" and the three-cleaner radius test "expected 0, actual 5". GREEN: 128/128 focused unit, 18/18 on PostgreSQL. Mutations of the floor term and of either window edge are caught.
  - B006: RED was "recipient reads expected 2, actual 5" (SQLite) and "expected 2, actual 4" (PostgreSQL), and a failed recipient read returned credit first. GREEN: 63/63 unit and 55/55 PostgreSQL.
- **AC3.** Receipt `raw/backend/wave-d-measurement-receipt-001.json` (`e69a5c00…`). n=5 per cohort, ABBA, with a fresh restore and seed each run. Before is `5f94129`, after is `026bf52`.
  - Digest: SQL 724 → 74; elapsed median 861 → 352 ms (p90 875 → 355); allocation 31.6 → 11.2 MB.
  - Cleanup: SQL 1,010 → 13; elapsed median 2,010 → 1,307 ms (p90 2,140 → 1,315); allocation 224.3 → 209.3 MB.
  - Outcomes are identical within and across cohorts, with 0 provider attempts.
  - Limits are listed in the report: local loopback, not idle; no peak-memory reading on macOS; empty batch reads on this fixture; the zero-credit sweep only.
- **AC4.** On the final code (`026bf52`, fast-forwarded onto `64dfed7`):
  - Release build: 0 errors.
  - Unit: 8,639/8,639.
  - Integration: 848/848 under `en_US.UTF-8`, as CI runs it; the machine culture fails the three known `CollectedFeeSharePayTests` only.
  - Host: 488/488.
  - Repository checkers: 11/11. `check-module-boundaries` ran in the worktree that has web `node_modules`, at `05ac271`, with 0 drift.
  - Docs build: passes.
  - The graph is refreshed.
- **AC5.** Correctness, security (S1/S8/S11), optimizer and architect reviews all approve with notes; none blocks. The optimizer ran `EXPLAIN (ANALYZE, BUFFERS)` of the batch statement on a wide band of the audit copy and found no plan risk. It also confirmed the predicted counts (74 and 13) before the measurement ran.
