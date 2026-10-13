---
id: T-0863
title: Open a retention scope and archive gate for each company task
size: S
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: ["0064", "0072"]
layers: [backend, db, docs]
security_touching: true
---

# Open a retention scope and archive gate for each company task

## Context

T-0841 retention child. DataRetentionBackgroundService :66-86 uses one scope across companies/tasks and RunSafe catches each task (:93-105), while tasks commit tracked deletes. IArchiveWriteGate is scoped and currently opened on the outer scope (:64). Both the original and fresh remaining-jobs verifiers require every company/task child scope to open its own gate; LegalObligationGateCallSiteTests pins the call to this file.

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

- Keep GetAllIdsAsync companies-in loop; RunSafeAsync opens an async child for every (tenant,task) pair, sets that child's company before filtered/config reads, resolves the existing retention service/dependencies there and invokes its private task method.
- Change any selector/delegate to invoke the freshly resolved child service; a method-group from parent this would keep parent dependencies and fails the scope requirement.
- Open child IArchiveWriteGate.OpenForLegalObligation("data retention") inside DataRetentionBackgroundService for each task; dispose the opening before its child on success, exception and cancellation. No outer gate can substitute for this child gate.
- Preserve task durable batching/blob-failure paging and legal purposes; prove later same-company tasks plus other-company work remain independent. Remove only this deviation; no new type/enum/task registry.

## NOT doing

- No new type, retention duration/category/erasure policy or archived-company guard redesign.
- No deletion of orphaned DEV blobs/queues and no shared DEV operation.
- No loss of companies-in tenancy pattern or widened archive-write permission.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [ ] **AC1** — Two companies with different settings, one frozen: refuse tracked PII/dispute commit in one task; later same-company task and other-company tasks persist without dirty deletes/anonymization leaking. Real isolation assertions go RED then GREEN.
- [ ] **AC2** — Each company/task has distinct DbContext and ArchiveWriteGate instances, correct tenant-local cutoffs and its own scoped legal opening. Ordinary frozen-books writes remain refused, authorized retention succeeds, and a later child after disposal has a closed gate. A single outer gate cannot make the pin pass.
- [ ] **AC3** — Existing legal-obligation call-site guard remains pinned to DataRetention file without weakened allowlist; batching/blob-error/cancellation controls, full backend suites, S8 review and exact deviation removal have evidence. No new retention abstraction/schema/client work was introduced.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: CLAUDE companies-in retention loop with child task scopes and scoped archive write gate.

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
