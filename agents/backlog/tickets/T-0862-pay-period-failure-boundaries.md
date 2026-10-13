---
id: T-0862
title: Isolate employee invoice failures and fail unsafe period-close staging
size: M
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: ["0046", "0072"]
layers: [backend, docs]
security_touching: true
---

# Isolate employee invoice failures and fail unsafe period-close staging

## Context

T-0841 pay-period child covers both the per-employee invoicing loop (PayPeriodBackgroundService :240-317; called commits :457/:502/:520) and the staging-only period-close loop (:156-172). Invoicing writes invoices, payout references and cash set-offs, not an external payout. ADR-0046’s close/email ordering remains controlling.

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

- Isolate each employee's invoice/email work on the existing service with child-local dependencies/UoW/reference allocator under the period operator. Re-read employee and preserve per-currency/reference-before-document semantics.
- Pass immutable period ID/dates/close instant, never the parent's tracked period graph. Employee commits must not persist the parent period's staged close; keep parent close outside those child commits and group-commit it only after email work, preserving ADR-0046.
- Drop the per-period close/read/successor staging catch without adding a type. Preserve deliberately handled email/PDF failures only when they cannot flush dirty leftovers later; propagate failed PDF metadata commits out of the child. An invoice-failure plain-email fallback starts from a clean dependency scope after disposal of the failed invoice child.
- Prove employee poison/isolation, both PDF metadata branches, parent-close boundary and the staging-only close failure; remove both precise deviation entries.

## NOT doing

- No payroll totals/rates/pay-cycle, payout transfer, invoice/cash set-off meaning or ADR-0046 ordering change.
- No extra command type solely for the staging-only period close, no attempt cap or payroll framework.
- No backend schema/client regeneration implied by this scope-only change.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [ ] **AC1** — A's invoice commit repeatedly refuses after invoice/order-pay/cash staging; B persists only its invoice/reference/cash-setoff and receives its own committed document. Both PDF-success metadata and PDF-error-recording commit faults are covered. Real persisted isolation assertions are RED then GREEN.
- [ ] **AC2** — During employee child commits, another connection still sees parent period Open; the child does not track its close. Close persists only in the post-email parent/group boundary. No-worked-pay rollover and ADR-0046 ordering stay intact.
- [ ] **AC3** — Stage period close then throw in successor/read staging: tick fails before group commit and a fresh tick still sees Open. Baseline swallowed/Closed behavior is RED; no-catch behavior GREEN. No new staging-only type.
- [ ] **AC4** — Two currencies, one invoice per employee/period/currency, operator reference series, cash set-offs/deactivated-company rules and clean-scope fallback preserve records without claiming money transfer. Existing duplicate-email/crash trade is named; security review/full suites and both deviation removals are recorded.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Fresh employee service scopes; no-catch period-close staging boundary.

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
