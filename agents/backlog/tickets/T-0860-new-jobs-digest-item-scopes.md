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

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final object/record/docs checks and PR publication are complete in #331. Master Backend has the one subsequently observed assertion failure below; it remains a completion gate for T-0860 and the parent. The inherited six B10 test-fixture hits remain unchanged; the broader raw checker did not pass.

- 2026-10-13 — PR [#331](https://github.com/VM-s-Solutions/cleansia/pull/331) merged at `2026-10-13T21:38:51Z` as `26883f31c2cba99881ffe2140ae197e46d438670`. Tested head `4929ed84967d154b8ee3f90a13cf091dde822688` and the merge have tree `7d1e079731705440c78e733b93503e771217747c`. All **6/6 exact-head checks in five workflows** passed on first attempts, with no rerun; the SHA-guarded merge published that head. The PR contains 110 changed files and 13 commits: #330 closure first, eleven separate child implementations, then a forward record-label correction leaving the complete source tree unchanged. Saved annotations/job logs and CI/merge/deployment-metadata reads are bound by `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-remaining-pr331-ci-merge-closure-001.json` SHA256 `3db0c81a36eccd6a5aebda2bede326fe2919117160d0ea2613343b37dbd8f96f`. Its bounded GETs found no deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. No phase reset or deployment was dispatched.

- 2026-10-13 — The eleven actual child commit objects passed **33/33 docs gates** (references, catalog claims and VitePress at each head). The final forward head passed all three docs gates and the history secret check (2,459 scanned commits, no leak); affected lint selected zero projects and adds no positive lint coverage. Independent actual-object verification checked complete archive bytes/modes and the cumulative source identities; final record refutation was CLEAN. Receipts under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/`: `head-gates/actual-commit-docs-gates.json` SHA256 `bae651def190f5daa4460d46aad7524c041d468349f2f4faff061558ed898493`, `local/precision-committed-head-gates-001.json`, and `verifier/actual-commit-gates-verification.json` SHA256 `86ecfca9ead2ad53bc45e515f5768f99170d8a801fb04444ed48875fafd1c65b`.

- 2026-10-13 — Master push attempt 1 at `26883f31c2cba99881ffe2140ae197e46d438670` passed Docs and Secret scan, but Backend failed: **8,731 unit passed; 974/975 PostgreSQL integration passed; host tests did not execute after that failure**. The sole failed test is T-0860's `Every_Child_Uses_One_Clock_While_Notices_Follow_Their_Recipient`: both saved outbox-key tick suffixes were `639275250997934015`, while the reloaded database stamp was `639275250997934010`. The exact diagnostic and all three job logs/annotation responses are retained by `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-ci-evidence-attempt1-receipt-001.json` SHA256 `28ee98b87f224974941c6ab8cd11fecfe28eb7525b10b42f8b5683ac0245a350`. No rerun was dispatched. T-0860 and the parent remain in progress pending a focused assertion repair and green master; no green-master claim or native runtime proof is made. This observed failure supplements the earlier PR/local success record rather than replacing its evidence.

- 2026-10-13 — A focused forward repair addresses this child's newly observed master assertion failure. The test now requires one **exact distinct original tick suffix** across both push messages, then compares its microsecond-normalized value with the one persisted watermark. The per-message suffix, recipient/operator, count and non-null checks remain; production clocks, message keys, handlers, schema and clients are unchanged. Exact source SHA256 `b9d82c4da15b093bdf69adc528333d08e520060f47644607289b6485cc2400bc`. The failure diagnosis was independently confirmed; the failed master job was not rerun.

- 2026-10-13 — Command-only precision evidence uses a distinct exact-image loopback PostgreSQL **16.15** container and typed Npgsql parameters: all ten submicrosecond remainders round-trip under the repaired assertions, whereas the original comparison rejects nine valid round-trips. Independent checks also reject differing raw clocks within one microsecond, shifted stamps, wrong tenants and malformed/noncanonical keys. The macOS pre-fix class passed **2/2**, explicitly a characterization; the saved Linux master failure is the actual RED. The repaired class passed **2/2**, followed by five independent runs with **10/10** cases; the ordinary inherited `postgres:latest` fixture was actually **18.6**, separate from the pinned PG16 probe. The first private probe readiness attempt stopped before any .NET probe ran, and its own container/credentials were cleaned; the corrected TCP readiness attempt passed.

- 2026-10-13 — Exact-source implementation review returned **APPROVED** and separate security verification returned **PASS**, no material finding. Own implementation verification executed two PostgreSQL cases, ten precision cases and fifteen negative controls. Security verification performed 23 controls and recomputed evidence hashes; it inspected the separate executed runtime proof and did not claim its own runtime run. Under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-failure-001/`: `developer/developer.json` SHA256 `4fd9389c3096510aab26531247f46f5b042f63582835be47179cc0cb6bbc9b72`, `reviewer.json` SHA256 `d98638e485c949f27306722a5de29624ae9b6e0e1109b281d33bf96c19e2af09`, and `security.json` SHA256 `efa15abdca5326e52dd236ccd397340dff60b35b9593b72d703cc5294a0375ba` bind the exact source and immutable evidence.

- 2026-10-13 — The complete repair source passed fresh CI-order local gates: forced Release Rebuild, **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped; frontend **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, eight typecheck-guard tests, all three uncached builds and **5 customer + 1 partner + 1 administrator** browser smokes; Android forced compile followed by **2,818 cases / 304 XML suites**, zero failures/errors/skips. Requested compile/test tasks executed; lifecycle-only UP-TO-DATE lines add no coverage. Full receipt `full-local-gates-001.json` SHA256 `68175bc188dba25c562c851589a2485daf858da4378e9e7992f45b33a8eb4830` retains all 17 commands/exits/log hashes and source-before/after equality. Affected lint selected zero projects, so no positive lint coverage is claimed. No new iOS run is asserted for this backend-test-only diff.

- 2026-10-13 — The branch carries #331's truthful closure first at `efe6e765ab7acd76e99fa6037c025e408daeb810`; its records passed fresh refutation and all three docs gates against the exact committed source (`closure-docs-gates-003.json` SHA256 `d153ce372ca00324bb99edf38e3f94b92e1a38c87c881b2934c8631ee924847b`). Two earlier archive-preparation assertions stopped before docs execution: tar permission normalization and gradlew.bat line-ending conversion were corrected by reconstructing exact Git blobs/modes. No migration or client regeneration was needed. Final committed-head docs/history checks, exact-head CI, guarded merge and green master remain due; this repair adds no device, real-mail, DEV or PRO proof.

- 2026-10-13 — Fresh final evidence verification returned **PASS_FOR_ALLOCATED_LOCAL_REPAIR_EVIDENCE**, no material finding. It independently checked 1,152 evidence hashes, 7,525 source/SQL inputs, all actual suite counts and produced-DLL/TRX paths. The 29 produced DLL timestamps lie inside the forced rebuild interval. No root before-DLL snapshot was collected, so unchanged root DLL hashes during those suites are not claimed. Receipt `final-gate-verifier.json` under the same repair folder, SHA256 `c20084aa4c5f8f9825784c0c6c408ef486c23424abba5cb7f6a46a91502d5fb7`. Final committed-head gates and publication remain separate obligations.
