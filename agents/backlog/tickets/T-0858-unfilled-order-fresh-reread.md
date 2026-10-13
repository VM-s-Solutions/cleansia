---
id: T-0858
title: Re-read an unfilled order before claiming its cancellation
size: S
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0854]
blocks: []
stories: []
adrs: ["0067", "0071", "0072"]
layers: [backend, docs]
security_touching: true
---

# Re-read an unfilled order before claiming its cancellation

## Context

T-0841's stale-crew child. CancelUnfilledOrders :131-138 only inspects its earlier tracked graph; a take onto an already Confirmed crewless order does not change its status token (TakeOrder :406-412). Decision 3 preserves pre-end late take, so the guard remains needed. The P2 contact report preserves this sweep’s separate cancellation purpose.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This is the separately filed job child of T-0841. Answer 8 approves that parent; its verifier-correction section requires the split, no-catch preference, no new staging-only types and the specific dependency/gate rules.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/contact-verifier-before.md`

## Doing

- Immediately before CleanerNoShowCancellation.ExecuteAsync, make a narrow AsNoTracking projection by order ID and operator tenant of fresh CurrentStatus and crew existence/count. Skip missing rows, tenant mismatches, any fresh crew and status outside New/Confirmed.
- Keep existing per-order commit/no-catch behavior; no reload/reattach of an unrelated graph or new command type.
- Correct the stale re-check comment and prove the race on two PostgreSQL connections with a paid crewless Confirmed order starting 31 minutes ago whose slot end remains in the future. A concurrent take leaves status Confirmed, so the existing token is not mistaken for the new guard.
- Name the residual re-read-to-cancellation/refund-claim commit window without claiming serializability.

## NOT doing

- No TakeOrder lock/token, Orders xmin, conditional write or new concurrency architecture.
- No slot-start gate, 30-minute grace/168-hour lookback/money qualification change, or new cancellation policy.
- No caller’s own-list or administrator behavior change; no DTO/schema/client regeneration.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [ ] **AC1** — Given a paid crewless Confirmed order selected by the sweep and taken on another connection before its final re-read, the sweep skips it: no Cancelled track, refund/credit return or cancellation notice. The real race goes RED then GREEN.
- [ ] **AC2** — A started order is likewise skipped; a genuinely unfilled qualifying order still follows the existing grace/lookback/refund rules. Pre-end late-fill behavior remains after T-0854.
- [ ] **AC3** — Separate money/security review records the remaining re-read-to-claim window; updated comment, relevant existing suites and full backend checks have saved evidence.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: T-0854. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Inline fresh untracked guard before existing no-show cancellation service.

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
