---
id: T-0860
title: Isolate a cleaner digest failure from later digest work
size: M
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

# Isolate a cleaner digest failure from later digest work

## Context

T-0841 new-jobs digest child. NewJobsDigestService :110-244 catches/continues around work whose watermark commit is at :376. Employee concurrency tokens can refuse that commit. The fresh remaining-jobs report confirms the source and identifies near-start candidates that disappear before the next hourly tick; this justifies per-cleaner scopes. No runtime or future diff approval is claimed.

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

- Give each cleaner a child scope on the existing service, preferring a private one-cleaner method and only the minimal concrete scoped registration/alias needed. Near-start candidate windows justify this independence; no new mediator command by default.
- Select IDs/detached facts, capture one common sweepStartedAtUtc, re-read eligible cleaner and watermark in the child, adopt employee tenant before tenant-filtered work, and use the original instant for freshness bounds/message subject.
- Preserve country/radius/hold/overlap/mute/future-only behavior. Stamp the same child-owned tracked employee used for processing rather than a nullable second post-notice load; a disappeared employee stages nothing. No-jobs leaves watermark unchanged; muted qualifying cleaner still advances it.
- Prove A's repeated refused commit does not lose B's near-start notice/watermark this invocation; remove only this deviation.

## NOT doing

- No digest future-only policy, cadence/radius/hold/currency, notification copy or offerability redesign.
- No attempt cap, generic digest framework or new abstraction without a current need.
- No duplicate implementation of T-0854’s central rule.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — A stages a notice and its real employee-token/constraint commit is repeatedly refused; B has a job that starts before the next hourly tick. B persists notice/outbox/watermark now, and no A rows leak. The unfixed failure sequence is recorded RED and the isolated result GREEN.
- [x] **AC2** — With A's fault cleared, a fresh eligible re-read sends once. One common sweep instant, child-local employee/context, two-company watermark ownership and explicit recipient tenants are verified through persisted records; disappearance stages no notice without watermark.
- [ ] **AC3** — Radius, tenant watermark, empty/muted digest, expired-hold/released-overlap and host-stop controls remain; no digest policy/clock framework is introduced. Full backend suites, side-effect security review and exact deviation removal are recorded.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Existing scoped service/private one-cleaner method; child owns its reads, watermark, pending dispatch and commit.

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

- 2026-10-13 — On the remaining-jobs branch after #330's closure `3440446a43929843440d384df17a0fd049b41e4a`, the parent digest selects IDs and resolves a fresh concrete digest service per cleaner. Each child reloads current employee/account/operator facts and owns its context, repositories, tenant override, pending notification staging and commit. A single captured sweep instant is passed to all children; the existing slot-end rule, freshness/hold/released-overlap, radius, muted/no-job and subject policy remain. The Functions registration uses a concrete service with a same-scoped interface alias. No schema, HTTP DTO/endpoint, generated client or clock framework changed.
- 2026-10-13 — Genuine isolated PostgreSQL baseline RED `t0860-red-pg-03`: **0 passed / 1 failed** at B's durable watermark assertion. An ordered real EmployeeRepository query forces A before B; a competing real RegistrationNumber update preserves Approved eligibility and causes repeated commit refusal. Baseline leaves both watermarks null and no durable feed/outbox. The initial Countries.CreatedBy seed failure is NONPROOF; the B-before-A baseline pass is characterization, not a defect RED. Final forced Release GREEN `t0860-green-pg-03` passed **7/7**, and unit/startup/timer/ColdPath controls passed **56/56**, zero skips. Final rebuild had zero errors. Earlier constructor-arity and strict-mock/disposal/tenant-stamp fixture failures remain recorded separately.
- 2026-10-13 — Fixed A refuses twice while B's own watermark/feed/outbox persists within the first sweep. Cleared-fault fresh execution gives A one notice while still eligible; no retry after its job window expires is promised. A separate persisted case proves one common sweep watermark/subject and recipient company distinct from employee/operator. Actual rows, named results, source manifests, commands, TRXs and pre-cleanup binary hashes are under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/T-0860/`; `READY.md`, `final-blobs/` and `final-blobs-sha256.json` freeze all twelve owned files. The PreferredHold blob contains only the digest adaptation; the later lapsed-offer constructor adaptation is sequenced separately. Earlier nonproof/characterization binaries removed before fingerprinting have no assembly-identity claim. The exact digest deviation row was retired. AC3's separate side-effect/security review and final combined gates remain open; these focused checks do not claim merge evidence.

- 2026-10-13 — Independent implementation review approved the exact frozen 44-file digest/payroll/retention source set. Its own forced Release build passed with zero errors; final unit **183/183** and PostgreSQL **80/80**, zero failed/skipped. Private item-body no-ops failed all five intended watermark/invoice/retention assertions; byte-exact restoration and a fresh forced rebuild restored the full scoped green results. The source/command/DLL/TRX/row identities are saved at `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/reviewer-round1.md`. Scoped implementation approval excludes current cumulative records/docs, full final-head gates, CI and merge. Separate security verification remains underway.

- 2026-10-13 — Separate security verification passed the exact frozen scope changes and updated T-0857 fixture: its fresh 60-file input combines the 44 digest/payroll/retention paths with the unchanged reviewed money inputs and the final one-file fixture override. Own **208 unit**, **80 scoped PostgreSQL** and **2 T-0857** cases passed, zero failed/skipped, after forced Release rebuilds. These are separate executed filters, not final combined 77-path/full-suite approval. Source/DLL/TRX/row fingerprints and scoped PASS are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/security-round1.md`; only owned build outputs were removed afterward. Final combined suites, per-job/head checks, record refutation, CI and merge remain pending.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen 44-file digest/payroll/retention source set. Its own forced Release build passed with zero errors; final unit **183/183** and PostgreSQL **80/80**, zero failed/skipped. Private item-body no-ops failed all five intended watermark/invoice/retention assertions; byte-exact restoration and a fresh forced rebuild restored the full scoped green results. The source/command/DLL/TRX/row identities are saved at `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/reviewer-round1.md`. Scoped implementation approval excludes current cumulative records/docs, full final-head gates, CI and merge. Separate scoped security verification is PASS; the final result is recorded below.

Separate security verification passed the exact frozen scope changes and updated T-0857 fixture: its fresh 60-file input combines the 44 digest/payroll/retention paths with the unchanged reviewed money inputs and the final one-file fixture override. Own **208 unit**, **80 scoped PostgreSQL** and **2 T-0857** cases passed, zero failed/skipped, after forced Release rebuilds. These are separate executed filters, not final combined 77-path/full-suite approval. Source/DLL/TRX/row fingerprints and scoped PASS are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/scopes/security-round1.md`; only owned build outputs were removed afterward. Final combined suites, per-job/head checks, record refutation, CI and merge remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final record/head checks, CI and merge remain pending.
