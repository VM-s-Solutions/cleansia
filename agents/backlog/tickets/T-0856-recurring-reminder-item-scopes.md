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

- [ ] **AC1** — An occurrence that throws or suffers a refused commit leaves no committed stamp/notice; a later occurrence in the same dispatch still commits its own stamp and outbox under its own tenant. Isolation assertions are RED before and GREEN after.
- [ ] **AC2** — A two-connection PostgreSQL payment race on A after its fresh unit read refuses only A, while B succeeds; A retry carries no leftovers. A repeating fault never contaminates a neighboring scope.
- [ ] **AC3** — Fresh re-check excludes paid/terminal/already-reminded/outside-window/no-user rows, including a short-notice occurrence already stamped by materialization. Host cancellation stops dispatch and Failed accounting/logging reflect actual failed units.
- [ ] **AC4** — Only reminder catalog deviation/allowlist entries change; full backend suites and separate notifications/state/security review have actual evidence at the combined T-0840/T-0856 head.

## Dependencies and execution

Phase: **PR 2**. Dependencies: T-0840. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: AutoCancelStaleRecurringOrders dispatcher + AutoCancelRecurringOccurrence pipeline-committed unit.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
