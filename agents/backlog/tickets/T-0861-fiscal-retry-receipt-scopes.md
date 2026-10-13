---
id: T-0861
title: Give each fiscal retry receipt its own scope
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

# Give each fiscal retry receipt its own scope

## Context

T-0841 fiscal retry child. FiscalRetryService :40-115 commits at :66/:85/:94 then catches/Rollback/continues at :102-112. Its claim-first email commits are deliberate and must survive the scope fix. The fresh remaining-jobs report confirms the source and recommends existing service-level child scopes; no behavior execution receipt is claimed.

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

- Select untracked receipt IDs/detached data; create each child on the existing fiscal service with a private one-receipt method, avoiding a new self-committing mediator handler.
- Fresh child re-read includes Language/Receivable, requires FiscalCode null and due FiscalNextRetryAt, adopts row tenant before order/services, and re-checks receipt/order operator agreement. Preserve the existing selection predicate rather than adding email reconciliation.
- Preserve three explicit service-level durable commits: fiscal state, then EmailSent/guest-token claim before external send, then best-effort provider metadata. Catch/log/count after discarding a failed child; active cancellation propagates. No Rollback-and-reuse within it.
- Prove real child/context isolation at all three boundaries. Existing fake-UoW durability tests do not supply isolation RED; the specifically mislabelled claim test faults fiscal commit 1 and does not prove email-claim commit 2 or next-tick selection.
- Remove this exact deviation. If a mediator unit is nevertheless needed, document its narrowly named explicit-commit exception; the existing scoped service approach avoids adding that exception.

## NOT doing

- No tax/provider/receipt state, email claim-first, retry cadence or attempt-cap policy change.
- The separately reported fiscal email-retry selection/test-label defect is excluded: no reconciliation-query fix or acceptance obligation for it in this wave.
- No general permission to call CommitAsync in command handlers and no generic fiscal unit framework.
- No other pre-commit email migration; T-0843 owns Started/Completed.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — With real separate contexts for receipts A/B/C, B's fiscal-state commit refuses after staged fiscal work; A/C durably record only their permitted results, with no B rows carried into C. Repeat B fault. Prove actual unfixed RED or explicitly characterize a non-RED sequence and add a scope-ownership pin tied to it.
- [x] **AC2** — Fault B's actual second email-claim commit: no B email is sent before its durable claim and C still completes its own claim/send. Fault third metadata commit: B's already durable claim prevents second email while C remains healthy. Do not claim B is selected on the next tick after fiscal state succeeded.
- [x] **AC3** — Two-company child-local provider/config/order ownership, fee receipts, fiscal enforcement matrix, stable registration token and host cancellation remain. Full suites, S7/S8 review and catalog removal have evidence. The separately reported fiscal email-retry predicate/test-label finding is excluded from this ticket's fix/AC.

## Dependencies and execution

Phase: **Remaining-jobs PR**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: T-0841. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Fresh scope per receipt with the existing claim-first service boundary.

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

- 2026-10-13 — After closure `3440446a43929843440d384df17a0fd049b41e4a`, fiscal retry now selects detached IDs and resolves the existing concrete fiscal service per receipt in a fresh scope. The child reloads eligibility/receipt/order operator agreement, adopts its own tenant and owns its ReceiptService/repositories/UoW/tokens/blob/email dependencies. Host cancellation propagates; registered/moved-later candidates skip. The concrete/interface registration aliases the same scoped instance. Existing durable fiscal-state C1, claim-before-email C2 and metadata C3 boundaries and stable fiscal key remain; no generic pipeline fiction, schema, HTTP contract or client change was introduced.
- 2026-10-13 — Actual final isolated baseline RED **12 executed / 7 passed / 5 failed**: ownership expected four contexts/observed one, swallowed host cancellation, receipt/order mismatch and registered/moved-later candidate cases fail. The three real B commit-boundary/repeated-fault sequences and fiscal mode/fee controls already passed baseline and are explicitly characterization. Final forced Release GREEN passed **23/23 integration**, then initial **32/32 unit** and wider **74/74 fiscal/Functions unit** checks, zero skips; the wider run is not added to the initial run as a distinct-test total. Real ReceiptService/UoW/guest-token and persisted rows are used, with scoped external seams and a stable-key fake authority ledger. Preliminary fixture setup failures are retained as NONPROOF.
- 2026-10-13 — Six exact source/test/registration files, binary fingerprints, commands/TRXs/named failures and DB reads are frozen under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/money/T0861/`, documented by `implementation-evidence.md`, `final-manifest.json` and `test-counters-and-db-reads.json`. The exact fiscal deviation row was retired. The separately reported old fiscal email-claim test-label/selection gap is untouched: after fiscal code persists, the current retry selector does not promise later automatic email reconciliation. No new finding ticket or dispatch was added. The later independent three-child money security pass returned scoped PASS (own85PG+242unit); implementation review/full combined gates/merge remain due.

- 2026-10-13 — Independent implementation review approved the exact frozen three-child money source, with its own 85 PostgreSQL and 242 unit tests passing, zero failed/skipped. Six private no-op checks failed as intended; byte-restored source then passed all 22 new PostgreSQL cases after a forced rebuild. Separate security verification passed its own 85 PostgreSQL and 242 unit tests, zero failed/skipped. These group counts are not added together as distinct tests. Reports and exact source/command/TRX identities: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/money/reviewer-round1.md` and `money/security-round1.md`. Scoped approval excludes final cumulative records, combined suites, exact-head CI and merge, which remain pending.
- 2026-10-13 — Independent raw-output verification confirms all three property-selected durable-boundary cases already passed on unfixed source; ownership/cancellation/operator/selected-row assertions supply the five genuine REDs. The actual selector in the frozen `OrderReceiptRepository.GetDueForRetryAsync` requires no fiscal code and a due retry instant. Thus after C1 succeeds, a refused C2/C3 is not promised next-tick email reconciliation. Scoped disposal-before-continuation is source-traced; the disposal recorder proves eventual context disposal, not its timing before C. AC1 explicitly permits this characterization plus ownership RED; AC2's real durable-before-send and metadata-boundary assertions pass. AC3 stays open for full combined gates.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Independent implementation review approved the exact frozen three-child money source, with its own 85 PostgreSQL and 242 unit tests passing, zero failed/skipped. Six private no-op checks failed as intended; byte-restored source then passed all 22 new PostgreSQL cases after a forced rebuild. Separate security verification passed its own 85 PostgreSQL and 242 unit tests, zero failed/skipped. These group counts are not added together as distinct tests. Reports and exact source/command/TRX identities: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/money/reviewer-round1.md` and `money/security-round1.md`. Scoped approval excludes final cumulative records, combined suites, exact-head CI and merge, which remain pending.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final object/record/docs checks and PR publication are complete in #331. Master Backend has the one subsequently observed assertion failure below; it remains a completion gate for T-0860 and the parent. The inherited six B10 test-fixture hits remain unchanged; the broader raw checker did not pass.

- 2026-10-13 — PR [#331](https://github.com/VM-s-Solutions/cleansia/pull/331) merged at `2026-10-13T21:38:51Z` as `26883f31c2cba99881ffe2140ae197e46d438670`. Tested head `4929ed84967d154b8ee3f90a13cf091dde822688` and the merge have tree `7d1e079731705440c78e733b93503e771217747c`. All **6/6 exact-head checks in five workflows** passed on first attempts, with no rerun; the SHA-guarded merge published that head. The PR contains 110 changed files and 13 commits: #330 closure first, eleven separate child implementations, then a forward record-label correction leaving the complete source tree unchanged. Saved annotations/job logs and CI/merge/deployment-metadata reads are bound by `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-remaining-pr331-ci-merge-closure-001.json` SHA256 `3db0c81a36eccd6a5aebda2bede326fe2919117160d0ea2613343b37dbd8f96f`. Its bounded GETs found no deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. No phase reset or deployment was dispatched.

- 2026-10-13 — The eleven actual child commit objects passed **33/33 docs gates** (references, catalog claims and VitePress at each head). The final forward head passed all three docs gates and the history secret check (2,459 scanned commits, no leak); affected lint selected zero projects and adds no positive lint coverage. Independent actual-object verification checked complete archive bytes/modes and the cumulative source identities; final record refutation was CLEAN. Receipts under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/`: `head-gates/actual-commit-docs-gates.json` SHA256 `bae651def190f5daa4460d46aad7524c041d468349f2f4faff061558ed898493`, `local/precision-committed-head-gates-001.json`, and `verifier/actual-commit-gates-verification.json` SHA256 `86ecfca9ead2ad53bc45e515f5768f99170d8a801fb04444ed48875fafd1c65b`.

- 2026-10-13 — Master push attempt 1 at `26883f31c2cba99881ffe2140ae197e46d438670` passed Docs and Secret scan, but Backend failed: **8,731 unit passed; 974/975 PostgreSQL integration passed; host tests did not execute after that failure**. The sole failed test is T-0860's `Every_Child_Uses_One_Clock_While_Notices_Follow_Their_Recipient`: both saved outbox-key tick suffixes were `639275250997934015`, while the reloaded database stamp was `639275250997934010`. The exact diagnostic and all three job logs/annotation responses are retained by `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-ci-evidence-attempt1-receipt-001.json` SHA256 `28ee98b87f224974941c6ab8cd11fecfe28eb7525b10b42f8b5683ac0245a350`. No rerun was dispatched. T-0860 and the parent remain in progress pending a focused assertion repair and green master; no green-master claim or native runtime proof is made. This observed failure supplements the earlier PR/local success record rather than replacing its evidence.
