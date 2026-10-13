---
id: T-0841
title: Give every system job that continues past a failed commit one scope per item, SendRecurringOrderReminders and RedrivePendingRefunds first, and make CancelUnfilledOrders re-read the order it cancels
size: L
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0856, T-0857, T-0858, T-0859, T-0860, T-0861, T-0862, T-0863, T-0864, T-0865, T-0866, T-0867]
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

**The deviations** were read from the tree by the ruling, with a search for `catch (Exception` and `Rollback()` under `src/Cleansia.Core.AppServices`. Lines are as of `207d2f319`.
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

## Doing — approved split, 2026-10-13

This is the aggregate evidence record for twelve children, not one L implementation job.

- T-0856: recurring reminder item scopes; PR 2 with T-0840.
- T-0857: refund re-drive item scopes; remaining-jobs PR after T-0851/M10.
- T-0858: unfilled-order fresh re-read; after T-0854.
- T-0859: membership notification fail-tick boundary.
- T-0860: new-jobs digest cleaner scopes.
- T-0861: fiscal retry receipt scopes.
- T-0862: employee invoicing plus period-close staging.
- T-0863: retention company/task scopes and child archive gate.
- T-0864: pre-cleaning reminder fail-tick boundary.
- T-0865: await lapsed-offer notice before stamp and fail unsafe staging.
- T-0866: cleaner reminder fail-tick boundary.
- T-0867: tomorrow digest fail-tick boundary.

Each child owns its observable failure boundary and evidence. The remaining-jobs PR is explicitly allowed. Prefer no catch where the existing window supports retry; the four notification staging sites and payroll close get no new types. A scoped unit resolves all dependencies afresh, re-reads its row, and takes tenancy from that row. Retention opens its archive gate in each child. CancelUnfilledOrders re-reads status/crew immediately before cancellation and retains the final read-to-claim race residual. Product windows, attempt caps, stale-checkout company commits and refund rules remain unchanged. Do not claim a next-tick guarantee at a finite window edge. Fiscal e-mail reconciliation is separately reported, outside this scope.

Remove only each verified job’s deviating-forms row; the final child reconciles the aggregate entry and ADR-0072’s temporary statements. Full combined suites and independent reviews are final-head obligations.

## NOT doing

- The stale-checkout sweep keeps one commit per company. It is the reference shape, and its two-operator tests pin it.
- No attempt caps. The job-units decision record accepts their absence.
- The reminder sweep's window and status term (T-0840).

## Acceptance criteria (done looks like)

- [ ] **AC1** — Each listed job’s child proves its chosen independent-scope or fail-tick boundary RED then GREEN. A fail-tick shape need not finish later unsaved items in that tick; its retry eligibility and finite-window limitations are recorded.
- [ ] **AC2** — T-0858’s PostgreSQL two-connection test proves the final fresh read skips cancellation after a take committed before that read. The read-to-claim residual remains explicit; no serializability claim.
- [ ] **AC3** — All twelve child results are linked, the verified deviations and temporary records match the final tree, and the full combined suites and reviews pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The approved split replaces the earlier aggregate proposal. T-0844’s foundations shipped in #327; its remaining AC2/final DEV evidence is not supplied by this filing. The corrected source reports are `p2/correctness-verifier-before.md` and `remaining-jobs/verifier-before.md` under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. No acceptance result is claimed here.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 3 and 4, and its recommendation under BE-13. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work, and when.

- 2026-10-13 — First child T-0856 is implemented locally with T-0840: ordered reminder IDs, a new scope/command for each unit, fresh tenant/status/window/stamp/account reread, failed count and host-cancellation propagation. Actual isolation/poison-repeat and timing RED→GREEN evidence is in that child's Work record. The remaining eleven children are still future work in the explicitly permitted following PR; RedrivePendingRefunds remains after T-0851. This parent is a cross-reference/aggregate closure record, not an L implementation job. No remaining-job acceptance criterion or final catalog retirement is claimed here.

## Review

Pending.

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.

- 2026-10-13 — T-0856 is merged. The other eleven child changes are now the remaining-jobs branch scope; this parent remains in progress.
