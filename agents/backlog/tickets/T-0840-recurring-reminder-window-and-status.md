---
id: T-0840
title: Remind every recurring occurrence, morning slots included, and never one already cancelled
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, bicep, docs]
security_touching: false
---

## Context

The 2026-10-12 design ruling on BE-11 to BE-14 reported these as items 1 and 2. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_be-lead.json`. Both items were found by reading the code, not by running it, and the lines below were re-read at `207d2f319`. **Of that ruling's reported items, this one has the most effect: it can cost customers their recurring bookings.**

- **A morning gap.**
  - `SendRecurringOrderReminders` runs once a day, at 02:30 UTC (`deploy/bicep/main.bicep:531`; `src/Cleansia.Functions/appsettings.json:3`, `0 30 2 * * *`).
  - Each run selects occurrences 6 to 26 hours ahead (`SendRecurringOrderReminders.cs:22`, `:44-45`, `:56-57`). That is from 08:30 the same day to 04:30 the next.
  - So no run's window covers a slot between 04:30 and 08:30 UTC: 05:30 to 09:30 Czech time in winter, 06:30 to 10:30 in summer. That slot never gets the "confirm and pay" push.
  - This sweep is the only sender of `recurring.scheduled`.
- **What follows from the gap.** An occurrence nobody confirms is cancelled an hour before its slot (`AutoCancelStaleRecurringOrders.cs:32`, `:66`, `:76-80`). A customer whose schedule sits in the gap is never asked to confirm, so every occurrence is cancelled.
- **A cancelled occurrence can be reminded.**
  - The query (`:51-59`) has no `CurrentStatus` term, unlike `Order.AwaitsCustomerConfirmation` (`Order.cs:569-573`). Its comment (`:49-50`) says it uses that term.
  - So an occurrence that was cancelled while still Pending, and not yet reminded, gets a push asking the customer to confirm it.

## Doing — approved 2026-10-13

- **Cover every slot** with the hourly :30 cron (`0 30 * * * *`) and the 2–26-hour window. Established occurrences receive one notice about 25–26 hours ahead; the sent-at stamp prevents repeats.
- A late-created valid occurrence whose confirmation cutoff is at or before the next strict :30 tick receives its one short-notice reminder staged with materialization, in the existing atomic commit. T-0856’s periodic unit skips the stamp. No change to the confirmation cutoff, auto-cancel grace or quiet hours.
- **Add the status term** (not Cancelled, not Completed), so the query matches `AwaitsCustomerConfirmation`, as its comment says.
- **Tests.**
  - RED then GREEN: over a simulated day of scheduled runs, a 06:00 UTC slot is reminded exactly once.
  - RED then GREEN: a Pending occurrence that is already cancelled is not reminded.
  - The sweep's idempotency tests and `TimerCronSettingsAreDeployedTests` stay green.
- **Docs.** Update the timer table (`docs/architecture/infrastructure.md:208`) and any page that states the reminder's lead time.

## NOT doing

- The item-scope isolation is tracked separately by T-0856, in this same PR; this ticket owns reminder timing and status.
- No change to the auto-cancel's one-hour grace, or to the confirm cutoff.

## Acceptance criteria (done looks like)

- [x] **AC1** — The hourly :30 schedule and 2–26-hour window cover every eligible established slot, including the former 04:30–08:30 UTC gap; the sent-at stamp prevents repeated reminders. Timing/window assertions and stamp/replay controls pass with recorded RED/GREEN.
  - The cron test walks hourly fires across two days; delivery and already-stamped replay are separate tests, not a claimed full-day dispatcher simulation. A valid late-created occurrence whose next strict :30 tick reaches or passes the two-hour cutoff receives its single reminder in the materializer commit, and the periodic unit skips its stamp.
- [x] **AC2** — A cancelled or completed occurrence gets no reminder. Both terminal cases have real PostgreSQL RED/GREEN evidence.
- [x] **AC3** — The full local Release backend unit, integration and host suites pass, and the docs state hourly :30, the 2–26-hour window and the materializer short-notice path. Publication and deployment gates are recorded separately.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Decision 5 sets the hourly :30 cron and 2–26-hour reminder window, with CurrentStatus excluding Cancelled/Completed. T-0856 owns item-scope isolation in this PR. The fresh source check also identifies a valid delayed materialization whose confirmation cutoff precedes the next :30 tick; its one short-notice reminder must be staged with materialization when that next tick is at or after the cutoff. The confirm cutoff, auto-cancel grace and quiet hours stay unchanged.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 1 and 2. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

- 2026-10-13 — Implemented the approved reminder timing/status behavior in the P2 working tree. Mike's authority remains verbatim and dated above: "Analyze and Fix those" and "Go with defaults (recommended)". The runtime default in `SendRecurringOrderReminders.Command`, Functions `appsettings.json`, Bicep and `TimerScheduleConfigTests` agree on `0 30 * * * *` and the 2–26-hour window. The dispatcher excludes Cancelled/Completed; its fresh unit re-checks `AwaitsCustomerConfirmation`. Established eligible occurrences are first considered about 25–26 hours ahead and their persisted sent-at stamp prevents another periodic notice.
  - `MaterializeRecurringBookingTemplate` computes the next **strictly later** :30 tick. If that tick is at or after the occurrence's two-hour confirmation cutoff, it stages the existing `recurring.scheduled` notice and sent-at stamp inline, before the existing occurrence/template commit. Prague 08:00 and 08:30 fixtures at 03:55 UTC prove both the late and equality boundaries. A refused materializer commit persists no occurrence, materialization marker, feed or outbox intent; a later periodic unit skips an already-stamped short-notice occurrence. The confirmation cutoff, auto-cancel grace and quiet hours are unchanged. Per-occurrence scope isolation is recorded on T-0856.
  - **RED/GREEN:** `p2/correctness/reminders-timing-red-003` exited 1 with **5 failed / 25 executed** (20 passed), covering the hourly/low-bound/late-stamp changes. `reminders-isolation-red-002` exited 1 with **3 failed / 3 executed**, including both terminal-status cases. `reminders-green-unit-001` passed **36/36** and `reminders-green-postgres-001` passed **3/3**, both exit 0. The later combined correctness groups passed **207/207** unit (`correctness-unit-final-002`) and **31/31** PostgreSQL (`correctness-postgres-final-001`), exit 0. Exact commands, cwd, timestamps, receipt/log hashes and counts are in `p2/correctness/correctness-evidence-index-final.json`; behavior/source detail is in `behavior-record-final.md` beside it. Earlier compile, fixture, stale-assembly, disk-full and Docker setup attempts are retained as nonproof.
  - **Docs:** `docs/architecture/infrastructure.md` describes the hourly/window timing, and `docs/flows/booking-and-pricing.md` describes that timing plus the late path; obsolete timer/materializer comments are corrected. No endpoint/DTO/schema change or client/spec regeneration is owed by this behavior change.
- **Combined local Release validation, 2026-10-13:** after the combined Release build, the full backend suites ran from `src/` in unit → PostgreSQL integration → host order: **8,722/8,722**, **906/906**, **509/509**, all exit 0, no skipped or unexecuted cases. Receipts and TRXs are under the evidence root at `p2/local/backend-unit-full-002`, `backend-integration-full-001` and `backend-host-full-001`, with matching `.json`/`.log` files. These are local command results. Final frontend/Android/iOS and documentation gates, record refutation, exact-head CI, PR publication and merge are not asserted by this entry. No real customer email, live Stripe payout, shared DEV reset/deployment or PRO operation was used for these checks.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

Implementation assessment: **approve-with-notes**. The separate security verdict is **APPROVED for the reviewed P2 security implementation**, dated 2026-10-13. Evidence: `p2/reviewer-round1.md` and `p2/security-round1.md` under the evidence root. Their focused command results are distinct from the later full backend receipts above. Their implementation verdicts retain the limits recorded here; neither is a CI, merge or deployment result. Final records and publication gates remain outstanding.

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.
