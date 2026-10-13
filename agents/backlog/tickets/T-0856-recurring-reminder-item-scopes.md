---
id: T-0856
title: Let each recurring reminder commit or fail in its own scope
size: S
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0840]
blocks: []
stories: []
adrs: ["0072"]
layers: [backend, docs]
security_touching: true
---

# Let each recurring reminder commit or fail in its own scope

## Context

T-0841's reminder child. SendRecurringOrderReminders tracks every occurrence in one scope, commits at :83 and catches/continues at :86-91; a refused payment race can carry leftovers into later items. The fresh correctness report confirms this source and the AutoCancelRecurringOccurrence dispatcher/unit exemplar. Timing and the materializer short-notice guarantee belong to T-0840; this child isolates the periodic delivery and skips already stamped occurrences.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This is the separately filed job child of T-0841. Answer 8 approves that parent; its verifier-correction section requires the split, no-catch preference, no new staging-only types and the specific dependency/gate rules.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/correctness-verifier-before.md`

## Doing

- Read IDs only using T-0840’s hourly/window/status terms, ordered by CleaningDateTime then Id; dispatch each from its own CreateAsyncScope and count failures. Rethrow host cancellation.
- Use one reminder command unit that loads fresh across the tenant filter, re-checks AwaitsCustomerConfirmation, no sent-at stamp, user and the supplied window, adopts the row tenant, then stages reminder/stamp. Pipeline owns commit; no handler catch/CommitAsync.
- Move push-subject allowlist entry to the unit and include failure count in the handler success log; remove only this job’s deviating-forms row.
- Remain compatible with T-0840’s materializer-staged short-notice reminder: an already stamped occurrence produces no periodic duplicate.

## NOT doing

- No reminder cadence/lead/cutoff/grace decision here; those are T-0840.
- No conversion of other T-0841 jobs, no shared catch after failed unit commit or new generic dispatcher framework.
- No DTO/schema/client regeneration.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — An occurrence that throws or suffers a refused commit leaves no committed stamp/notice; a later occurrence in the same dispatch still commits its own stamp and outbox under its own tenant. Isolation assertions are RED before and GREEN after.
- [x] **AC2** — A two-connection PostgreSQL payment race on A after its fresh unit read refuses only A, while B succeeds; A retry carries no leftovers. A repeating fault never contaminates a neighboring scope.
- [x] **AC3** — Fresh re-check excludes paid/terminal/already-reminded/outside-window/no-user rows, including a short-notice occurrence already stamped by materialization. Host cancellation stops dispatch and Failed accounting/logging reflect actual failed units.
- [x] **AC4** — Only reminder catalog deviation/allowlist entries change; full local Release backend suites and the separate notifications/state/S7/S8 assessments have actual evidence at the combined T-0840/T-0856 working tree. Final publication gates remain separate.

## Dependencies and execution

Phase: **PR 2**. Dependencies: T-0840. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: AutoCancelStaleRecurringOrders dispatcher + AutoCancelRecurringOccurrence pipeline-committed unit.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

- 2026-10-13 — Implemented the approved reminder item isolation in the P2 working tree. Mike's verbatim 2026-10-13 authority, "Analyze and Fix those" and "Go with defaults (recommended)", is preserved above. `SendRecurringOrderReminders` selects IDs ordered by CleaningDateTime then Id and dispatches each `SendRecurringOrderReminder.Command` from its own `CreateAsyncScope`. Its response and success log include actual failed units. Host-requested cancellation propagates; an individual failure is counted/logged and its disposed scope cannot carry tracked leftovers into the next occurrence.
  - The unit loads the occurrence afresh across the tenant filter, then re-checks confirmation eligibility, sent-at stamp, customer and supplied window. It takes the operator from the loaded row, stages the existing reminder notice before the sent-at stamp, and leaves commit to the existing UnitOfWork pipeline. It has no handler catch or `CommitAsync`. Paid/terminal/outside-window/no-user/already-reminded rows stage nothing, including a short-notice occurrence stamped by T-0840's materializer. Only this job's allowlist/deviating-form roster entries move or retire; other T-0841 job conversions remain their own children. No generic dispatcher type, DTO/schema or client regeneration was introduced.
  - **RED/GREEN:** `p2/correctness/reminders-isolation-red-002` exited 1 with **3 failed / 3 executed** on the original shared-scope/terminal behavior; `reminders-green-postgres-001` passed **3/3**, exit 0. The final **31/31** PostgreSQL group (`correctness-postgres-final-001`) includes the added repeat-fault and host-cancellation cases. In the two-company payment race, A's refused commit leaves no stamp/feed/intent while B commits its own stamp and tenant-stamped feed/outbox; retry of paid A stages no leftovers. A repeat fault does not poison B. Host cancellation leaves no intent. The **36/36** reminder unit group and later **207/207** combined unit group also pass. Receipt/log hashes, actual filters, times and counts are in `p2/correctness/correctness-evidence-index-final.json`. Earlier failed setup or compile attempts are nonproof.
- **Combined local Release validation, 2026-10-13:** after the combined Release build, the full backend suites ran from `src/` in unit → PostgreSQL integration → host order: **8,722/8,722**, **906/906**, **509/509**, all exit 0, no skipped or unexecuted cases. Receipts and TRXs are under the evidence root at `p2/local/backend-unit-full-002`, `backend-integration-full-001` and `backend-host-full-001`, with matching `.json`/`.log` files. These are local command results. Final frontend/Android/iOS and documentation gates, record refutation, exact-head CI, PR publication and merge are not asserted by this entry. No real customer email, live Stripe payout, shared DEV reset/deployment or PRO operation was used for these checks.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

Implementation assessment: **approve-with-notes**. The separate security verdict is **APPROVED for the reviewed P2 security implementation**, dated 2026-10-13. Evidence: `p2/reviewer-round1.md` and `p2/security-round1.md` under the evidence root. Their focused command results are distinct from the later full backend receipts above. Their implementation verdicts retain the limits recorded here; neither is a CI, merge or deployment result. Final records and publication gates remain outstanding.
