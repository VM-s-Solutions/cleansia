---
id: T-0867
title: Fail a tomorrow digest tick instead of saving partial staging
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

# Fail a tomorrow digest tick instead of saving partial staging

## Context

T-0841 staging-only child. SendTomorrowJobDigest :138-153 catches each item before group commit :155. Fresh source verification gives local hour >=18 and <21 against an hourly tick. A later attempt at 18→19 or 19→20 can qualify; 20→21 cannot. No new unit type is authorized.

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

- Remove unsafe per-item staging catches; let a failed tick end and its scope be disposed before a future attempt.
- Keep hourly cadence, three-hour catch-up, recipient/tenant/date and stamp/idempotency logic. Prove a retry while the existing window remains open.
- Remove its precise deviation; when the final child lands, close/shrink the overall T-0841 catalog entry and make the agreed ADR-0072 mechanism/exception note. Sequence attribution wording with PR 6.

## NOT doing

- No new type, attempt cap, catch-up/day/cadence/customer-copy or generic digest scheduler change.
- No alteration of other children’s verified behavior; no role attribution in new ADR/comment text.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Existing commit boundary

Keep the four notification sweeps' existing tenant/company grouping and company commits in place. Remove only the unsafe item staging catch; a failure prevents the current group's unsaved commit and ends the invocation. A company committed earlier remains durable. Do not add handler commits, a final-company deferred commit, independent continuation within the failed group, or a new type.

## Acceptance criteria

- [ ] **AC1** — Two eligible cleaners in one company: A stages and B recipient read fails. Baseline catches/commits A and succeeds; fixed tick throws before group commit and no current group's unsaved stamps/feed/outbox persist. Recorded RED then GREEN.
- [ ] **AC2** — Fresh local 18→19 and 19→20 attempts with fault cleared send/stamp once; repeat that evening sends none. Local 20→21 is outside window, explicitly demonstrating no guaranteed retry from the final attempt.
- [ ] **AC3** — Zero-count cleaners remain unstamped for jobs taken later; separate new-jobs watermark, timezone/DST/local-date subject, recipient ownership and earlier-company durability stay. Full suites/security review/exact deviation removal have evidence; no new type/window.
- [ ] **AC4** — If this is the last child to land, parent evidence links all twelve children and no unaccounted shared-scope/staging deviation remains; coordinated ADR-0072 temporary statement correction is accurately recorded without role attribution.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: No-catch staging tick + existing hourly catch-up window.

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
