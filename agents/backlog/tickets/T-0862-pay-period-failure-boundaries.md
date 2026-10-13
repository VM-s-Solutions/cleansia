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

- [x] **AC1** — A's invoice commit repeatedly refuses after invoice/order-pay/cash staging; B persists only its invoice/reference/cash-setoff and receives its own committed document. Both PDF-success metadata and PDF-error-recording commit faults are covered. Real persisted isolation assertions are RED then GREEN.
- [x] **AC2** — During employee child commits, another connection still sees parent period Open; the child does not track its close. Close persists only in the post-email parent/group boundary. No-worked-pay rollover and ADR-0046 ordering stay intact.
- [x] **AC3** — Stage period close then throw in successor/read staging: tick fails before group commit and a fresh tick still sees Open. Baseline swallowed/Closed behavior is RED; no-catch behavior GREEN. No new staging-only type.
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

- 2026-10-13 — Re-ground-truthed after PR #330 at `3440446a43929843440d384df17a0fd049b41e4a`. Each employee's invoice work resolves the existing service and its dependencies in a fresh scope under the period operator, using immutable period ID/dates/close instant/label. Its clean period graph remains Open during child commits. The parent closes only at its post-email group commit; successor/read staging now propagates a failure before that boundary. No new staging-only production type was added.
- 2026-10-13 — Genuine baseline `t0862-red-pg-04`: **5 failed / 0 passed**. Three actual PostgreSQL CHECK refusals (`23514`) cover cash-setoff/invoice commit, successful-PDF metadata and renderer-error metadata; healthy B has no durable invoice on unfixed source. The other cases prove swallowed successor-read failure and parent Closed during active host stop. The fixture removes every fault constraint in finally and verifies zero remains. Final `t0862-green-rebuild-02` forced Release rebuild exits 0; `t0862-green-unit-02` **95/95**, `t0862-green-pg-02` **33/33**, zero failures/skips. Independent connections see parent Open through invoice commits/email calls and Closed only after the final group commit. Failed successor staging leaves Open, then a fresh tick succeeds. During host stop, A's earlier invoice remains durable, parent stays Open and B has no invoice/email/upload.
- 2026-10-13 — Earlier `RED02` invoice cases are **NONPROOF**: the observer's filtered read failed before the intended CHECK refusal. Its successor case alone was genuine. Initial fixture/compiler and `GREEN01` observer/DROP-literal failures are retained as failed attempts; only corrected `RED04` and `GREEN02` supply the final five-case proof. No production body changed after the initial fixed implementation. Immutable 13-file source set, SHA256s, source snapshots, TRX and command receipts: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/T-0862/` (`READY.md`, `final-blobs-sha256.json`, `green-owned.json`).
- 2026-10-13 — The repeated invoice-commit fault reopens the same period in a fresh fixture context; this is controlled repeat characterization, not automatic retry of an already closed period. Both metadata faults retain A's already committed invoice/setoff while refused metadata stays absent. The failed child is disposed before a plain-email fallback resolves fresh dependencies, with no dirty invoice entries. Existing mixed currencies, reference series, math, blob/language, no-worked-pay rollover and company wind-down controls pass. These are invoice/reference/cash records, not a payout transfer. A crash after durable invoices but before the post-email close may repeat period email; earlier currency invoices survive a later currency failure. Both precise payroll deviation rows are removed. Separate implementation/security review and final combined suites/CI remain pending; AC4 stays open for those gates. No schema/DTO/client change, regeneration, migration, DEV/PRO or graph rebuild ran for this child.

- 2026-10-13 — Independent implementation review approved the exact frozen 44-file digest/payroll/retention source set. Its own forced Release build passed with zero errors; final unit **183/183** and PostgreSQL **80/80**, zero failed/skipped. Private item-body no-ops failed all five intended watermark/invoice/retention assertions; byte-exact restoration and a fresh forced rebuild restored the full scoped green results. The source/command/DLL/TRX/row identities are saved at `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/reviewer-round1.md`. Scoped implementation approval excludes current cumulative records/docs, full final-head gates, CI and merge. Separate security verification remains underway.

- 2026-10-13 — Separate security verification passed the exact frozen scope changes and updated T-0857 fixture: its fresh 60-file input combines the 44 digest/payroll/retention paths with the unchanged reviewed money inputs and the final one-file fixture override. Own **208 unit**, **80 scoped PostgreSQL** and **2 T-0857** cases passed, zero failed/skipped, after forced Release rebuilds. These are separate executed filters, not final combined 77-path/full-suite approval. Source/DLL/TRX/row fingerprints and scoped PASS are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/security-round1.md`; only owned build outputs were removed afterward. Final combined suites, per-job/head checks, record refutation, CI and merge remain pending.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 reflection fixtures, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen 44-file digest/payroll/retention source set. Its own forced Release build passed with zero errors; final unit **183/183** and PostgreSQL **80/80**, zero failed/skipped. Private item-body no-ops failed all five intended watermark/invoice/retention assertions; byte-exact restoration and a fresh forced rebuild restored the full scoped green results. The source/command/DLL/TRX/row identities are saved at `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/reviewer-round1.md`. Scoped implementation approval excludes current cumulative records/docs, full final-head gates, CI and merge. Separate scoped security verification is PASS; the final result is recorded below.

Separate security verification passed the exact frozen scope changes and updated T-0857 fixture: its fresh 60-file input combines the 44 digest/payroll/retention paths with the unchanged reviewed money inputs and the final one-file fixture override. Own **208 unit**, **80 scoped PostgreSQL** and **2 T-0857** cases passed, zero failed/skipped, after forced Release rebuilds. These are separate executed filters, not final combined 77-path/full-suite approval. Source/DLL/TRX/row fingerprints and scoped PASS are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/security-round1.md`; only owned build outputs were removed afterward. Final combined suites, per-job/head checks, record refutation, CI and merge remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final record/head checks, CI and merge remain pending.
