---
id: T-0857
title: Re-drive each pending refund in a separate scope
size: M
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0851]
blocks: []
stories: []
adrs: ["0071", "0072"]
layers: [backend, docs]
security_touching: true
---

# Re-drive each pending refund in a separate scope

## Context

T-0841's money-moving child. RedrivePendingRefunds currently wraps :120-207 and commits at :206, then Rollback/continues at :208-218 in one scope. Source evidence comes from the original analysis/verifier, not a new execution here. T-0851 must land first because it fixes this refund seam’s stale paid-key ordering.

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

- Select refund IDs in CreatedOn/Id order with one captured minimum-age and alert cutoff/sweep instant; dispatch each through a separately scoped Command unit.
- In the child, load an untracked refund/order projection past the tenant filter, re-check Pending/AppRefund/age and a real row tenant, adopt that tenant, then load mutable work through child repositories. Recompute cancellation-own-key/re-drive/alert decisions from fresh facts.
- Preserve the Stripe transport catch, succeeded-total-before-row ordering, keyed money/credit/alert behavior and no second notice when resolved to an already settled refund. Discard a failed child; do not Rollback-and-reuse it.
- Land only after T-0851 and its M10 follow-up merge; refresh that actual refund seam before implementation. Prove isolation with real row/outbox state, not the old mocked-seam Rollback call count; remove only this deviation.

## NOT doing

- No T-0851 money fix reimplementation, refund token or payout/credit/status/age/alert policy change.
- No client/schema change or generic re-drive framework; company wind-down commits stay outside this conversion.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [ ] **AC1** — Given two pending refunds, the first unit’s refused commit leaves the second able to re-drive and alert with clean tracked state; PostgreSQL assertions are RED then GREEN.
- [ ] **AC2** — Fresh-row eligibility/tenant and total-before-row ordering are covered; host cancellation is not converted to a normal item failure. Existing same/distinct-key and refund/credit evidence remains green after T-0851.
- [ ] **AC3** — Scope disposal and repeating-fault tests, S7 security review, full backend suites, catalog row removal and work receipts prove the final behavior without claiming provider method-call count equals payout count.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: T-0851. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Dispatcher and pipeline-committed RedrivePendingRefund command unit.

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
