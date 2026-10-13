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

- [x] **AC1** — Given a paid crewless Confirmed order selected by the sweep and taken on another connection before its final re-read, the sweep skips it: no Cancelled track, refund/credit return or cancellation notice. The real race goes RED then GREEN.
- [x] **AC2** — A started order is likewise skipped; a genuinely unfilled qualifying order still follows the existing grace/lookback/refund rules. Pre-end late-fill behavior remains after T-0854.
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

- 2026-10-13 — After #330's closure `3440446a43929843440d384df17a0fd049b41e4a`, added a narrow untracked Id/operator projection of current status and crew immediately before the existing no-show cancellation body. Missing, moved-operator, staffed and started rows skip that body. Existing grace/lookback/refund policy and TakeOrder code/locking are unchanged; a take/start may still commit between the fresh read and cancellation/refund claim. This guard does not claim serializability. No model, HTTP contract, client or production type changed.
- 2026-10-13 — Isolated untouched CancelUnfilled source with cumulative frozen Redrive foundation: final RED **8 executed / 2 passed / 6 failed**. The actual TakeOrder mediator arm runs the production validator/handler/UoW on another connection, succeeds with a seat/work-contract acceptance and unchanged Confirmed status, then baseline wrongly writes Cancelled plus a refund row/apology ledger and extra notices. The separate domain seat-commit arm is distinguished. Started cases show existing concurrency refusal; missing-row shows FK refusal; moved operator cancels. The expanded fixture's too-long user-id setup error is NONPROOF. Forced Release GREEN passed **34/34 integration** and **63/63 unit**, zero skips: actual take remains Confirmed with its seat, no refund/ledger/cancel track and exactly the legitimate pre-sweep take feed/outbox. Unchanged paid/cash orders still cancel once. No provider payout count is inferred.
- 2026-10-13 — Exact two files are frozen in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/money/T0858/final-blobs/`, with `final-manifest.json`, commands/TRXs/named counters/fresh DB reads in `implementation-evidence.md` and `test-counters-and-db-reads.json`. Local focused proof is complete; the later three-child money security pass is scoped **PASS** (own 85PG+242unit across the group), saved at `remaining-jobs/money/security-round1.md` under that scratch root. Independent implementation review and final combined gates/merge remain due.

- 2026-10-13 — Independent implementation review approved the exact frozen three-child money source, with its own 85 PostgreSQL and 242 unit tests passing, zero failed/skipped. Six private no-op checks failed as intended; byte-restored source then passed all 22 new PostgreSQL cases after a forced rebuild. Separate security verification passed its own 85 PostgreSQL and 242 unit tests, zero failed/skipped. These group counts are not added together as distinct tests. Reports and exact source/command/TRX identities: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/money/reviewer-round1.md` and `money/security-round1.md`. Scoped approval excludes final cumulative records, combined suites, exact-head CI and merge, which remain pending.
- 2026-10-13 — Independent raw-row verification of the actual mediator take distinguishes legitimate take effects: baseline has one crew seat, Cancelled, one refund, one apology ledger, two feed rows and three outbox rows; fixed source has one crew seat, Confirmed, no cancellation refund/ledger, one feed row and two outbox rows. The fixed one/two are the take's already committed notices, not zero-notice evidence.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen three-child money source, with its own 85 PostgreSQL and 242 unit tests passing, zero failed/skipped. Six private no-op checks failed as intended; byte-restored source then passed all 22 new PostgreSQL cases after a forced rebuild. Separate security verification passed its own 85 PostgreSQL and 242 unit tests, zero failed/skipped. These group counts are not added together as distinct tests. Reports and exact source/command/TRX identities: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/money/reviewer-round1.md` and `money/security-round1.md`. Scoped approval excludes final cumulative records, combined suites, exact-head CI and merge, which remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final record/head checks, CI and merge remain pending.
