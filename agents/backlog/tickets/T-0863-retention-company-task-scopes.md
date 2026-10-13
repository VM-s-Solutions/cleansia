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

- [x] **AC1** — Two companies with different settings, one frozen: refuse tracked PII/dispute commit in one task; later same-company task and other-company tasks persist without dirty deletes/anonymization leaking. Real isolation assertions go RED then GREEN.
- [x] **AC2** — Each company/task has distinct DbContext and ArchiveWriteGate instances, correct tenant-local cutoffs and its own scoped legal opening. Ordinary frozen-books writes remain refused, authorized retention succeeds, and a later child after disposal has a closed gate. A single outer gate cannot make the pin pass.
- [x] **AC3** — Existing legal-obligation call-site guard remains pinned to DataRetention file without weakened allowlist; batching/blob-error/cancellation controls, full backend suites, S8 review and exact deviation removal have evidence. No new retention abstraction/schema/client work was introduced.

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

- 2026-10-13 — Re-ground-truthed after #330 at `3440446a43929843440d384df17a0fd049b41e4a`. All fifteen existing tasks keep their company order. Each `(company, task)` gets an async child scope; its tenant override is set before resolving the existing concrete service, repositories and settings. The child opens its own scoped legal-obligation gate in the same DataRetention source file, closes it before scope disposal, and supplies no parent tracked state or service instance to its task. No registry/enum/framework or retention-policy change was added. Active host cancellation propagates before another task/effect, including the existing blob loops.
- 2026-10-13 — Genuine final baseline `t0863-red-pg-04`: **3 failed / 0 passed**. A competing context changes the real PaymentStatus token after A is tracked. Five refused PII saves in the old shared context leave B's old PII named and later disputes/notifications unprocessed. Settings cancellation is swallowed and later tasks/company are consulted; storage cancellation attempts the second receipt blob and returns no error. Final `t0863-green-rebuild-02` forced Release rebuild exits 0 (220 pre-existing warnings, zero errors); `t0863-green-unit-02` **36/36**, `t0863-green-pg-02` **40/40**, zero failed/skipped. Real fresh rows prove repeated A refusal on two ticks while B's eligible PII and both companies' later dispute tasks progress under their different settings. Clearing the fault then anonymizes A on a fresh tick. Frozen B's ordinary books write still refuses with a closed gate.
- 2026-10-13 — The final fixture records thirty distinct task contexts, gates, tenant providers and settings providers on the first tick. The tenant is adopted before construction; gates are closed at construction and after the task, open at settings reads; all contexts are disposed. Settings stop throws OperationCanceledException after four tasks started; storage stop throws TaskCanceledException after one deletion, leaves both receipt metadata rows unmarked and starts no later task/company. Existing windows/batching, blob-failure paging, shared-address, archived-write, legal call-site and actual Functions startup controls pass. Exact 21-file frozen source, compiled 44-file cumulative source manifest, commands/TRX/rows and binary fingerprints: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/T-0863/` (`READY.md`, `final-blobs-sha256.json`, `green-snapshot-02-source-manifest.json`, `t0863-green-pg-02-rows.jsonl`).
- 2026-10-13 — Baseline01 seed NOT NULL failure is NONPROOF; baseline02's address RESTRICT refusal is not stale-token proof. Baseline03 reaches the real token refusal; RED04 adds the storage-stop assertion. The fixture shares source addresses with younger orders to preserve the real census and isolate the token from that separate FK branch. GREEN02 adds identity/retry logging only; production is unchanged from GREEN01. Earlier failed/characterization attempts and source/DLL fingerprints remain. Permanent refusal still delays its own eligible old row. Blob deletion can precede failed/cancelled metadata; its unmarked row remains retryable. Existing bulk-write durability and guard semantics are unchanged. No schema/DTO/client/migration/generation, DEV/PRO or graph rebuild ran for this child.
- 2026-10-13 — This is the planned last source child. Its precise retention row and the exhausted temporary deviation rosters are retired; the persistent rule remains with its explicit guard scope. The per-company flow now describes company/task dependency and gate ownership, and ADR-0072 has a dated mechanism note. The later ADR-0071–0075 wording phase retains the separate attribution sweep. Implementation/security review, final combined suites, record refutation, exact-head CI and merge remain pending; this local record does not assert publication.

- 2026-10-13 — Independent implementation review approved the exact frozen 44-file digest/payroll/retention source set. Its own forced Release build passed with zero errors; final unit **183/183** and PostgreSQL **80/80**, zero failed/skipped. Private item-body no-ops failed all five intended watermark/invoice/retention assertions; byte-exact restoration and a fresh forced rebuild restored the full scoped green results. The source/command/DLL/TRX/row identities are saved at `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/reviewer-round1.md`. Scoped implementation approval excludes current cumulative records/docs, full final-head gates, CI and merge. Separate security verification remains underway.

- 2026-10-13 — Separate security verification passed the exact frozen scope changes and updated T-0857 fixture: its fresh 60-file input combines the 44 digest/payroll/retention paths with the unchanged reviewed money inputs and the final one-file fixture override. Own **208 unit**, **80 scoped PostgreSQL** and **2 T-0857** cases passed, zero failed/skipped, after forced Release rebuilds. These are separate executed filters, not final combined 77-path/full-suite approval. Source/DLL/TRX/row fingerprints and scoped PASS are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/security-round1.md`; only owned build outputs were removed afterward. Final combined suites, per-job/head checks, record refutation, CI and merge remain pending.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen 44-file digest/payroll/retention source set. Its own forced Release build passed with zero errors; final unit **183/183** and PostgreSQL **80/80**, zero failed/skipped. Private item-body no-ops failed all five intended watermark/invoice/retention assertions; byte-exact restoration and a fresh forced rebuild restored the full scoped green results. The source/command/DLL/TRX/row identities are saved at `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/reviewer-round1.md`. Scoped implementation approval excludes current cumulative records/docs, full final-head gates, CI and merge. Separate scoped security verification is PASS; the final result is recorded below.

Separate security verification passed the exact frozen scope changes and updated T-0857 fixture: its fresh 60-file input combines the 44 digest/payroll/retention paths with the unchanged reviewed money inputs and the final one-file fixture override. Own **208 unit**, **80 scoped PostgreSQL** and **2 T-0857** cases passed, zero failed/skipped, after forced Release rebuilds. These are separate executed filters, not final combined 77-path/full-suite approval. Source/DLL/TRX/row fingerprints and scoped PASS are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/security-round1.md`; only owned build outputs were removed afterward. Final combined suites, per-job/head checks, record refutation, CI and merge remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final object/record/docs checks and PR publication are complete in #331. Master Backend has the one subsequently observed assertion failure below; it remains a completion gate for T-0860 and the parent. The inherited six B10 test-fixture hits remain unchanged; the broader raw checker did not pass.

- 2026-10-13 — PR [#331](https://github.com/VM-s-Solutions/cleansia/pull/331) merged at `2026-10-13T21:38:51Z` as `26883f31c2cba99881ffe2140ae197e46d438670`. Tested head `4929ed84967d154b8ee3f90a13cf091dde822688` and the merge have tree `7d1e079731705440c78e733b93503e771217747c`. All **6/6 exact-head checks in five workflows** passed on first attempts, with no rerun; the SHA-guarded merge published that head. The PR contains 110 changed files and 13 commits: #330 closure first, eleven separate child implementations, then a forward record-label correction leaving the complete source tree unchanged. Saved annotations/job logs and CI/merge/deployment-metadata reads are bound by `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-remaining-pr331-ci-merge-closure-001.json` SHA256 `3db0c81a36eccd6a5aebda2bede326fe2919117160d0ea2613343b37dbd8f96f`. Its bounded GETs found no deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. No phase reset or deployment was dispatched.

- 2026-10-13 — The eleven actual child commit objects passed **33/33 docs gates** (references, catalog claims and VitePress at each head). The final forward head passed all three docs gates and the history secret check (2,459 scanned commits, no leak); affected lint selected zero projects and adds no positive lint coverage. Independent actual-object verification checked complete archive bytes/modes and the cumulative source identities; final record refutation was CLEAN. Receipts under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/`: `head-gates/actual-commit-docs-gates.json` SHA256 `bae651def190f5daa4460d46aad7524c041d468349f2f4faff061558ed898493`, `local/precision-committed-head-gates-001.json`, and `verifier/actual-commit-gates-verification.json` SHA256 `86ecfca9ead2ad53bc45e515f5768f99170d8a801fb04444ed48875fafd1c65b`.

- 2026-10-13 — Master push attempt 1 at `26883f31c2cba99881ffe2140ae197e46d438670` passed Docs and Secret scan, but Backend failed: **8,731 unit passed; 974/975 PostgreSQL integration passed; host tests did not execute after that failure**. The sole failed test is T-0860's `Every_Child_Uses_One_Clock_While_Notices_Follow_Their_Recipient`: both saved outbox-key tick suffixes were `639275250997934015`, while the reloaded database stamp was `639275250997934010`. The exact diagnostic and all three job logs/annotation responses are retained by `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-ci-evidence-attempt1-receipt-001.json` SHA256 `28ee98b87f224974941c6ab8cd11fecfe28eb7525b10b42f8b5683ac0245a350`. No rerun was dispatched. T-0860 and the parent remain in progress pending a focused assertion repair and green master; no green-master claim or native runtime proof is made. This observed failure supplements the earlier PR/local success record rather than replacing its evidence.
