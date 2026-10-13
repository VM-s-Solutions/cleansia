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

- [x] **AC1** — In one company, A stages and B's recipient read throws; baseline catches and saves A, while fixed tick throws before group commit. No current group's unsaved stamp/feed/outbox persists. Actual defect is RED then GREEN.
- [x] **AC2** — Cleared fault at a still-eligible 65→60 or 60→55-minute attempt sends/stamps once; repeat is deduplicated. A failure at lower edge 55 is characterized as a possible missed reminder, not guaranteed next-tick recovery. Existing [55,70] window/cadence stays.
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

- 2026-10-13 — On the same fresh remaining-jobs branch after closure `3440446a43929843440d384df17a0fd049b41e4a`, removed only the per-order staging catch. The existing company override/commit remains; a staging failure now exits before that company's commit and cannot erase a previously committed company. The already registered `TimeProvider.System` supplies the captured clock and deterministic elapsed-tick tests. No new production type, schema, HTTP contract, client, cadence or message copy was introduced.
- 2026-10-13 — Untouched-source isolated PostgreSQL RED: **2 failed / 0 passed**, showing caught/successful partial persistence in same-company and later-company recipient-fault cases. Forced Release GREEN: **7 passed / 0 failed** (four new persisted cases plus three existing tenant-stamp controls), then **54 passed / 0 failed** pre-cleaning/start-early/timer controls; no skips. Tests establish actual traversal/company order, current-company absence, earlier-company durability, reciprocal operator/recipient company ownership and exact notice key/body/envelope/payload tenants. A fresh fault-cleared 65→60-minute attempt sends once and a later repeat preserves the first stamp; 55→50 characterizes the final-attempt miss. Host cancellation propagates.
- 2026-10-13 — Per-phase command logs, TRXs, exact persisted row JSON/hashes, isolated source manifests, binary fingerprints and cleanup receipts are saved under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/staging/T-0864/`; `final-owned/` and `final-owned-sha256.json` freeze the four owned files. The pre-cleaning job's exact deviation row was removed from the current catalog. A poison item can still delay its current company until the reminder window expires; no guaranteed recovery after that edge is claimed. AC3's separate review/security and final combined gates remain open; local focused checks are not a merge result.

- 2026-10-13 — Independent implementation review approved the exact frozen five-child staging source: 49 PostgreSQL cases and 179 unit controls (172 plus 7 timezone cases), 228 passed, zero failed/skipped, with forced Release rebuilds. Separate security verification passed 49 PostgreSQL cases and 171 unit controls, 220 passed, zero failed/skipped. Source identities and own command/TRX evidence are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/staging/reviewer-round1.md` and `staging/security-round1.md`. These scoped verdicts do not certify cumulative records or the final combined head; full suites, record refutation, exact-head CI and merge remain pending.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen five-child staging source: 49 PostgreSQL cases and 179 unit controls (172 plus 7 timezone cases), 228 passed, zero failed/skipped, with forced Release rebuilds. Separate security verification passed 49 PostgreSQL cases and 171 unit controls, 220 passed, zero failed/skipped. Source identities and own command/TRX evidence are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/staging/reviewer-round1.md` and `staging/security-round1.md`. These scoped verdicts do not certify cumulative records or the final combined head; full suites, record refutation, exact-head CI and merge remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final record/head checks, CI and merge remain pending.
