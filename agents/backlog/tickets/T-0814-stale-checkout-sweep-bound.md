---
id: T-0814
title: Bound each stale-checkout sweep to its oldest 500 checkouts
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

B006's second cause, as decided by Mike ("Go with recommended", Mike's rulings of 2026-10-11 (`/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/owner-decisions-2026-10-11.md`)). T-0808 listed three ways to bound the run; the ground truth found every trade-off note favouring (a), the existing `ExpireStaleCredit` idiom, and it is the only one that keeps each company's failure boundary [BE-22, D2 lane].

## Doing

- `CleanupStalePendingOrders` reads only the oldest `BatchSize` eligible checkouts: `.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Take(command.BatchSize)` on its one read. `BatchSize` is a command parameter defaulting to 500 and validated 1–5000, the `ExpireStaleCredit` shape.
- Grouping, the override reset, notify-first per company, the per-order work and one commit per company per run stay byte-identical. A backlog drains over the following 15-minute ticks with no cursor, because a processed order leaves the predicate.
- RED tests: a run touches at most `BatchSize`; the remainder goes on the next run; oldest first. Preservation: the sweep, two-operator, credit-return and producer suites.
- The n=5 interleaved measurement on the audit cleanup fixture (1,000 checkouts): SQL, allocation and the ticks to drain, before and after.
- Docs: `docs/flows/cross-cutting.md` and T-0808's NOT-doing note.

## NOT doing

- Detaching after each commit, or per-page commits (options b and c).
- Any change to failure boundaries: BE-11 to BE-14 are listed for Mike, not fixed here.

## Acceptance criteria (done looks like)

- [x] **AC1** — A run cancels at most `BatchSize` stale checkouts, oldest first, and a backlog drains across runs; RED then GREEN.
- [x] **AC2** — The existing sweep, two-operator and credit suites stay green.
- [x] **AC3** — The measurement records peak allocation per run and the ticks to drain, with outcomes identical across runs.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by the backend lane.
  - `CleanupStalePendingOrders` now reads `.OrderBy(o => o.CreatedOn).ThenBy(o => o.Id).Take(command.BatchSize)`.
  - The command is `Command(int OlderThanHours = 1, int BatchSize = 500)`, with `BatchSize` validated 1–5000. The timer sends the default 500.
  - Unchanged: grouping, the override reset, notify-first per company, the per-order work, and one commit per company per run.
  - RED:
    - The build fails first (CS1739 ×3).
    - Against a stub that accepts `BatchSize` and ignores it, the unit batch test fails (`Expected: 2 Actual: 4`).
    - Against the same stub, the validator accepts 0 and 5001, so those two theory rows fail (`Expected: False Actual: True`).
    - Against the same stub, the PostgreSQL two-operator test fails (`Expected: 3 Actual: 4`).
  - Mutations, each in a scratch copy and restored afterwards:
    - with no `Take`, both suites are RED;
    - with no `ThenBy`, or with no ordering at all, the unit suite is RED;
    - ordering by `Id` alone is RED in both suites.
  - GREEN:
    - unit: the sweep suite passes 23/23, and with the producer, credit-return, handler-smoke, timer-failure and push-subject suites, 56/56;
    - PostgreSQL: the two-operator suite passes 4/4, and with the guest e-mail, PII-retention and credit-return suites, 34/34.
  - AC3 was measured on a disposable audit clone with 1,000 stale checkouts, n = 5 per cohort, in ABBA order.
    - Before: one tick cancels all 1,000. It runs 13 SQL statements, tracks 5,000 entries, and allocates a median of 209.9 MB.
    - After: two ticks cancel 500 each. Each runs 11 SQL statements and tracks 2,500 entries. The median peak per run is 112.3 MB.
    - The final outcome is identical in all 10 runs.
    - Plan: `Limit 500` over an incremental sort over `IX_Orders_PaymentStatus_CreatedOn`, at 8.0 ms and 3,714 shared hits.
    - Elapsed times are not comparable, because the load average was 48–59.
    - Receipt (off-tree): `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/backend/t0814-measurement-receipt.json`, sha256 `97b6fd24…`.
- 2026-10-11 — Docs:
  - The backend lane updated `cross-cutting.md`.
  - The review fix pass reworded it, and updated `checkout.md` and `business-rules.md`. The `admin.order.new` row now ends "at most 500 a run; a backlog larger than 500 spreads over the following 15-minute runs".
- 2026-10-12 — Before the commit, the items the re-review still owed were added:
  - the `offerability.md` row;
  - a `CHANGELOG.md` Operators entry (Gate 7), because an operator sees the bound during a backlog.

  T-0808's NOT-doing list now points here.
- 2026-10-12 — Committed as `a9395e3`, which builds on its own.
  - The fix pass ran the full suites in CI form: unit 8,647/8,647; integration 850/850, in this Mac's decimal-comma culture with no `LANG` override; host 503/503.
  - Its integration and host runs came before its last edit, a doc comment in `HasOverlappingOrderTenancyAndScanFloorTests.cs`. Its unit run was repeated after that edit.
  - The re-review then ran all three suites (8,647, 850 and 503, all passing) on a mirror whose `src/` is byte-identical to this branch's head.
  - The records check ran them again on the committed T-0819 tree, the last .NET change on this branch: unit 8,647/8,647, integration 850/850 and host 503/503.
- 2026-10-12 — Shipped in [#322](https://github.com/VM-s-Solutions/cleansia/pull/322). Exact-head CI on `1136e16` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `6840882`. No deploy ran. Evidence: `raw/ci/p1-pr322-ci-merge-closure-001.json` (sha256 `d31e29ba…`) in the off-tree Wave F folder.

## Review

- **Correctness: approve-with-notes** (the re-review). It still owed three items: the CHANGELOG entry, the offerability row and the T-0808 pointer. They were applied before the commit, and the applied wording has not been re-reviewed.
- **Security: approve-with-notes.**
- **Liveness, noted, not a FAIL.** If one company's commit failed the same way on every run, and its rows were among the oldest 500, every company's backlog would stall.
  - No reachable trigger was found. `ArchiveCompany` refuses while any order is New. `CurrentStatus != Cancelled` rules out the re-cancel key collision.
  - During a backlog the sweep cancels 2,000 an hour. Credit spent at checkout comes back only when the sweep reaches its order.
- **Not absorbed:**
  - The sibling sweeps `AutoCancelStaleRecurringOrders` and `CancelUnfilledOrders` are still unbounded.
  - An exception in this sweep still skips `ReleaseOrphanedBenefitReservations` and `CancelUnfilledOrders` on the same tick.
  - Commits across companies now run in `(CreatedOn, Id)` order.
