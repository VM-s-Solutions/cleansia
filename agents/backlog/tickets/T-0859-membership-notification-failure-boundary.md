---
id: T-0859
title: Stop continuing a membership notification tick after a failed commit
size: S
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: ["0072"]
layers: [backend, docs]
security_touching: true
---

# Stop continuing a membership notification tick after a failed commit

## Context

T-0841 membership notification child. The two lifecycle loops commit at SendMembershipLifecycleNotifications :80/:118 and catch/continue in the same scope. UserMembership has xmin, so concurrent webhook writes can trigger refused commits. The original verifier recommends no catch first: the daily tick has a 2–4-day retry window. The fresh remaining-jobs source report confirms both loops and narrows the allowed fail-tick/retry claim.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This is the separately filed job child of T-0841. Answer 8 approves that parent; its verifier-correction section requires the split, no-catch preference, no new staging-only types and the specific dependency/gate rules.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh remaining-jobs source report: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier-before.md`. It observed b67c985b7 with no backend diff from 737bdb4 and executed no tests/builds/DB operations. Older per-job citations are corrected by that report where specified; re-ground-truth again at the eventual remaining-jobs head, after T-0851/M10.

## Doing

- Remove both per-membership catches and both manual item commits. Stage the two cohorts through the existing command and let its UoW pipeline commit a successful tick; no new type.
- Preserve memberships' existing tenant and explicitly recipient-owned notification/feed/outbox tenant. Do not add a final-company override/deferred ambient stamp. Cover two operators and a recipient whose current company differs from the membership operator.
- Preserve renewal [now+2d,now+4d], cancellation [now,now+2d], daily cadence, stamp/re-arm/idempotency and Active-only selection.
- Record bounded poison-item/window-edge residuals; remove only this job's deviation. A low-edge renewal or same-day-ending cancellation may no longer qualify next day; do not promise guaranteed retry.

## NOT doing

- No new unit/type merely to avoid a retryable tick failure; no generic notification engine.
- No membership billing, trial/lifecycle window, cadence, attempt cap or user-visible copy change.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [ ] **AC1** — With A staged and B's recipient read throwing through the real command pipeline, the tick fails and commits none of that tick's unsaved membership stamp/feed/outbox. Current caught/successful partial commit is RED; propagated failure/no commit is GREEN.
- [ ] **AC2** — A real membership xmin conflict makes the tick fail; a fresh next daily tick with fault cleared sends once for cohorts deliberately still eligible (renewal near +3d, cancellation near +1.5d). The lower-edge case is a characterization of possible missed retry, not a claimed GREEN recovery.
- [ ] **AC3** — Two companies, recipient/operator mismatch, Active-only, rollover re-arm, duplicate run and cancellation propagation preserve ownership/outcomes. No manual handler commits/new types remain for this child; separate security review/full suites and exact deviation removal have actual evidence.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Existing command UoW pipeline, no per-membership catch or manual item commit, no new type; finite-window residual is recorded.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Scope and verification limits

- A continuing item scope owns its reads, repositories/services, UoW, tenant provider, pending dispatch, internal durable commits and final commit. Pass IDs/immutable bounds, not parent tracked entities or parent-owned service instances; genuinely fresh projections use AsNoTracking or a fresh context.
- A dispatcher catches a non-cancellation item failure only after that child is discarded. Active host cancellation propagates before any further item/company/effect.
- Preserve operator business-row ownership and separately explicit notification recipient ownership; recipient company can differ from operator. Never defer tenant-stamped new business work under a final company override.
- Existing mock-UoW/empty-Rollback call-count tests are characterization, not automatically defect RED. Prove the concrete stale-state/constraint/concurrency failure against unfixed source; a sequence that stays green cannot be relabelled as RED. Persisted/outbox assertions use fresh contexts and real PostgreSQL where relevant.
- Retry evidence selects a next tick still within current eligibility. Lower-edge/window-expiry and poison-item delay residuals are recorded; no guaranteed next-tick recovery or window widening is claimed.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
