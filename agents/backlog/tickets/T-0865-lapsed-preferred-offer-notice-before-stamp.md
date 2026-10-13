---
id: T-0865
title: Stage the lapsed-offer notice before its stamp and fail unsafe staging
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

# Stage the lapsed-offer notice before its stamp and fail unsafe staging

## Context

T-0841 staging-only child with a lost-notice path. PreferredOfferClosedNotifier stamps first (:26) then calls NotifyAsync (:33); NotifyLapsedPreferredOffers catches at :96 and commits at :108. A recipient-read failure saves only the stamp, and the sweep never selects it again (:70). This is not merely a possible repeated push.

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

- Make the existing notifier method async and await NotifyAsync before marking the lapse stamp for account-owned orders. Moving a statement after the current returned task would be unreachable. Guests still stamp without a notice, preserving finite sweep selection.
- Remove the sweep's per-order catch, preserve group commit, and add no type. Preserve per-round subject and all three callers' existing atomic commits.
- Test the notifier directly as well as sweep/pipeline: dropping the catch alone must not hide an unchanged stamp-before-await bug through rollback. Remove the precise catalog deviation after verification.

## NOT doing

- No accepted keep/deviation alternative, permanent stamp-only loss or new unit type.
- No preferred-hold deadline/round/recipient/customer copy, lapse rule or notification event-key redesign.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Existing commit boundary

Keep the four notification sweeps' existing tenant/company grouping and company commits in place. Remove only the unsafe item staging catch; a failure prevents the current group's unsaved commit and ends the invocation. A company committed earlier remains durable. Do not add handler commits, a final-company deferred commit, independent continuation within the failed group, or a new type.

## Acceptance criteria

- [x] **AC1** — Direct notifier recipient lookup throws on an account order: baseline mutates stamp first, fixed notifier leaves stamp absent and no feed/outbox. Sweep/pipeline failure persists no stamp. Both tests prove RED then GREEN.
- [x] **AC2** — A fresh still-eligible tick sends/stamps once; guest stamps without notification; decline/sweep same-round collapses, next-round subject differs and hold/preferred identity is unchanged. After take/start/cancel, retry eligibility may cease; no guaranteed delivery is claimed.
- [x] **AC3** — Existing three callers' atomicity/tenant behavior, full suites/security review and exact deviation removal have evidence. A permanent capped-scan poison row can fail the tick under the governed no-catch choice; record that residual without inventing units. No new type.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Inline notice-before-stamp + no-catch staging tick.

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

- 2026-10-13 — Account lapsed-offer notification staging is now awaited before the existing lapse stamp. Guests still stamp/return without a notice. Removed the sweep's per-item catch, retaining company commits; a staging failure prevents the current group's unsaved rows from landing, while earlier committed companies remain. The existing System TimeProvider supplies the clock seam; the preferred identity, deadline, hold and round are unchanged, as are caller commit boundaries. No new production type/schema/HTTP contract/client/cadence was added.
- 2026-10-13 — Untouched-source isolated PostgreSQL RED **3 failed / 0 passed**: direct notifier local stamp had changed before recipient lookup failure; same/later-company sweeps caught the fault and saved stamp-only lost-notice state. Final forced Release GREEN passed **6/6 PG**, and existing lapse/hold/decline/take/subject controls **31/31**, zero skips. Fresh +5-minute eligible retry/dedup, guest exclusion, same-round collapse/later-round distinct subjects, earlier-company durability, explicit recipient/operator and envelope/payload ownership and host-stop are covered. The first unit compile failed on a sibling digest fixture dependency and executed no tests; it is retained as NONPROOF.
- 2026-10-13 — Commands, actual named failures/rows/hashes/TRXs, source and binary fingerprints are under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/staging/T-0865/`. Four exclusive files are frozen in `final-owned/`; the shared PreferredHold fixture has a compiled old-digest one-line blob in `shared-final-individual/` and an eventual cumulative blob in `shared-final-cumulative/`, sequenced after T-0860 with explicit patch/hash receipts. Only its exact deviation row was retired. A permanent poison row in the capped scan can fail the tick, and take/start/cancel can end eligibility before retry; no eventual-delivery guarantee is added. Later independent five-child security returned scoped PASS and implementation review APPROVED for the exact frozen implementation; final combined gates/merge remain due.

- 2026-10-13 — Independent implementation review approved the exact frozen five-child staging source: 49 PostgreSQL cases and 179 unit controls (172 plus 7 timezone cases), 228 passed, zero failed/skipped, with forced Release rebuilds. Separate security verification passed 49 PostgreSQL cases and 171 unit controls, 220 passed, zero failed/skipped. Source identities and own command/TRX evidence are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/staging/reviewer-round1.md` and `staging/security-round1.md`. These scoped verdicts do not certify cumulative records or the final combined head; full suites, record refutation, exact-head CI and merge remain pending.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen five-child staging source: 49 PostgreSQL cases and 179 unit controls (172 plus 7 timezone cases), 228 passed, zero failed/skipped, with forced Release rebuilds. Separate security verification passed 49 PostgreSQL cases and 171 unit controls, 220 passed, zero failed/skipped. Source identities and own command/TRX evidence are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/staging/reviewer-round1.md` and `staging/security-round1.md`. These scoped verdicts do not certify cumulative records or the final combined head; full suites, record refutation, exact-head CI and merge remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final object/record/docs checks and PR publication are complete in #331. Master Backend has the one subsequently observed assertion failure below; it remains a completion gate for T-0860 and the parent. The inherited six B10 test-fixture hits remain unchanged; the broader raw checker did not pass.

- 2026-10-13 — PR [#331](https://github.com/VM-s-Solutions/cleansia/pull/331) merged at `2026-10-13T21:38:51Z` as `26883f31c2cba99881ffe2140ae197e46d438670`. Tested head `4929ed84967d154b8ee3f90a13cf091dde822688` and the merge have tree `7d1e079731705440c78e733b93503e771217747c`. All **6/6 exact-head checks in five workflows** passed on first attempts, with no rerun; the SHA-guarded merge published that head. The PR contains 110 changed files and 13 commits: #330 closure first, eleven separate child implementations, then a forward record-label correction leaving the complete source tree unchanged. Saved annotations/job logs and CI/merge/deployment-metadata reads are bound by `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-remaining-pr331-ci-merge-closure-001.json` SHA256 `3db0c81a36eccd6a5aebda2bede326fe2919117160d0ea2613343b37dbd8f96f`. Its bounded GETs found no deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. No phase reset or deployment was dispatched.

- 2026-10-13 — The eleven actual child commit objects passed **33/33 docs gates** (references, catalog claims and VitePress at each head). The final forward head passed all three docs gates and the history secret check (2,459 scanned commits, no leak); affected lint selected zero projects and adds no positive lint coverage. Independent actual-object verification checked complete archive bytes/modes and the cumulative source identities; final record refutation was CLEAN. Receipts under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/`: `head-gates/actual-commit-docs-gates.json` SHA256 `bae651def190f5daa4460d46aad7524c041d468349f2f4faff061558ed898493`, `local/precision-committed-head-gates-001.json`, and `verifier/actual-commit-gates-verification.json` SHA256 `86ecfca9ead2ad53bc45e515f5768f99170d8a801fb04444ed48875fafd1c65b`.

- 2026-10-13 — Master push attempt 1 at `26883f31c2cba99881ffe2140ae197e46d438670` passed Docs and Secret scan, but Backend failed: **8,731 unit passed; 974/975 PostgreSQL integration passed; host tests did not execute after that failure**. The sole failed test is T-0860's `Every_Child_Uses_One_Clock_While_Notices_Follow_Their_Recipient`: both saved outbox-key tick suffixes were `639275250997934015`, while the reloaded database stamp was `639275250997934010`. The exact diagnostic and all three job logs/annotation responses are retained by `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-ci-evidence-attempt1-receipt-001.json` SHA256 `28ee98b87f224974941c6ab8cd11fecfe28eb7525b10b42f8b5683ac0245a350`. No rerun was dispatched. T-0860 and the parent remain in progress pending a focused assertion repair and green master; no green-master claim or native runtime proof is made. This observed failure supplements the earlier PR/local success record rather than replacing its evidence.
