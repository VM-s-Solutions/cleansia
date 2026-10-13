---
id: T-0864
title: Fail a pre-cleaning reminder tick instead of saving partial staging
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

# Fail a pre-cleaning reminder tick instead of saving partial staging

## Context

T-0841 staging-only child. SendPreCleaningReminders :87-112 catches per-item staging errors then group-commits at :119. Fresh source verification gives [55,70] minutes and a five-minute tick; at the lower edge another valid attempt is not guaranteed. No new type is authorized.

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

- Remove the per-item staging catch so a staging failure ends the tick without the later group commit saving partial work.
- Keep current selection/tenant/window/stamp/notice ordering and prove a subsequent valid tick retries inside the same window.
- Remove this job’s catalog deviation after verification.

## NOT doing

- No new unit command/type, notice window/cadence, copy, attempt cap or per-item catch continuation.
- No other T-0841 job conversion or generic reminder abstraction.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Existing commit boundary

Keep the four notification sweeps' existing tenant/company grouping and company commits in place. Remove only the unsafe item staging catch; a failure prevents the current group's unsaved commit and ends the invocation. A company committed earlier remains durable. Do not add handler commits, a final-company deferred commit, independent continuation within the failed group, or a new type.

## Acceptance criteria

- [ ] **AC1** — In one company, A stages and B's recipient read throws; baseline catches and saves A, while fixed tick throws before group commit. No current group's unsaved stamp/feed/outbox persists. Actual defect is RED then GREEN.
- [ ] **AC2** — Cleared fault at a still-eligible 65→60 or 60→55-minute attempt sends/stamps once; repeat is deduplicated. A failure at lower edge 55 is characterized as a possible missed reminder, not guaranteed next-tick recovery. Existing [55,70] window/cadence stays.
- [ ] **AC3** — Earlier committed company's notices survive later-company failure, with recipient ownership, selection and host-stop behavior preserved. Full checks/security review/exact deviation removal have evidence and no new type/group-commit redesign occurred.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: No-catch staging within existing tenant groups, preserving existing per-company commits and earlier-company durability.

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
