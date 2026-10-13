---
id: T-0854
title: Stop offering and taking an open seat when its booked slot ends
size: M
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0850]
blocks: []
stories: []
adrs: ["0037", "0057"]
layers: [backend, docs]
security_touching: true
---

# Stop offering and taking an open seat when its booked slot ends

## Context

The approved mike-5 rule is slot END, not start, midnight or two hours after start. OrderAvailability currently has no time conjunct. Double-booking uses raw EstimatedTime minutes (`TakeOrder.cs:326-329`, `OrderRepository.cs:367-369,490`). The fresh contact report also confirms two-hour board/dashboard floors hide 180/240-minute started jobs before the decided end.

## Owner decision — 2026-10-13

Mike, answer 5: "I don't think that it needs to be offerable".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This implements default decision 3: the raw booked slot ends at start plus estimated minutes. Started-job joining and the 30-minute late-fill opportunity remain.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/contact-verifier-before.md`

## Doing

- Add the same strict CleaningDateTime.AddMinutes(EstimatedTime) > nowUtc conjunct to the SQL expression factory and scalar C# rule, preserving existing status/money terms and separate seat/hold/currency checks. No crew-duration division.
- Thread one captured current instant and the start/duration scalar probe through actual consumers, including browse/take, specifications, dashboard and preferred/seat-open notices; preserve the own-assignment arm.
- Adjust/remove the two-hour board and dashboard floors only as needed to keep otherwise eligible long slots visible through their end. Keep any coarse scan bound only with a stated source/assumption and agreement test.
- Preserve CancelUnfilledOrders separate past-order selection, grace/lookback/money behavior; correct its late-fill explanation to name the end bound. Preserve administrative reassignment and existing customer/admin/assigned access.
- Publish a short dated owner-ruling record partially superseding ADR-0037 D1, with an ADR-0057-shaped dated head pointer and unchanged historical body. Reserve its ADR number at implementation. Update offerability, role card and business-rules formulas/tables.

## NOT doing

- No start-of-slot cutoff, day-based cutoff, crew-shortened duration or unsupported historic maximum invariant.
- No refund/cancellation-grace/lookback, cover/drop status, administrator cover event or wind-down reader redesign.
- No new error key, DTO, client/spec regeneration, schema or clock service; no old ADR body rewording.
- Digest future-only refinement and other independent purpose filters remain unless a concrete in-scope incompatibility is proved.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — Fixed-clock SQL/C# tests assert explicit expected ids before start, after start/before end, at end and after end for New cash and started Paid work; exactly at end is false. The end case is RED before implementation and GREEN after.
- [x] **AC2** — Real PostgreSQL agreement/surface tests show a 60-minute unfilled slot at +10 minutes remains offered/browsable/takeable, and a started 180/240-minute slot at +130 minutes remains on board, dashboard count/preview and browse/take until its raw end. This proves the old two-hour floors do not pre-empt the rule.
- [x] **AC3** — At/after end, a stale open-list Take adds no assignment and returns the existing refusal; seat-open notifier has zero recipients/candidate query. Existing assigned cleaner, owner customer and administrator access to ended rows remains. Holds/seat/currency/money controls stay intact.
- [x] **AC4** — Existing OrderStatusSetPredicatePlanTests remains green with its actual completed-dashboard/overlap scope accurately described; real availability agreement tests cover the changed board rule. New ADR, dated pointer, living pages and separate security/money review record the decided boundary and preserved late-fill behavior.

## Dependencies and execution

Phase: **PR 2**. Dependencies: T-0850. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Single OrderAvailability rule, SQL/scalar agreement and one captured time per consumer.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

- 2026-10-13 — The single SQL/scalar/entity availability rule now requires `CleaningDateTime.AddMinutes(EstimatedTime) > nowUtc`, using the raw booked minutes and one captured instant per consumer. Take projects all seven scalars; board/dashboard/browse/contract preview/pending offers and notices receive the clock. Removed the open-board/dashboard two-hour start floor; own/admin purpose filters and the digest's future-only refinement remain. CancelUnfilled retains its separate30-minute grace,168-hour lookback and empty/never-started/money predicates.
- 2026-10-13 — Command-only actual RED:5 failed/46 unit cases,3 failed/3 PostgreSQL agreement cases, and1 failed/1 long-started surface case. GREEN142/142 combined unit and29/29 PostgreSQL (5 availability agreement,16 scope,8 existing plan cases) in `p2/contact/implementation-receipt.md` and its fingerprints. Real fixtures derive capacity with `CalculateRequiredEmployees`, put another cleaner on180/240-minute started slots at+130 minutes, and prove board/dashboard/preview/browse/take before raw end. New60-minute work at+10 remains fillable. At/equal/after end, real pipeline Take adds no assignment: held work returns existing OrderNotFound, unheld work OrderNotTakeable. Assigned own lists/details/contracts, customer owner and administrator reads remain accessible. Ended seat notices select no candidate/recipient.
- 2026-10-13 — Accepted ADR-0076 records the dated ruling; ADR-0037 has only its dated head pointer, with the historical body preserved. Living offerability, role card, business rules and orders/booking pages carry the boundary. Existing OrderStatusSetPredicatePlanTests protect completed-dashboard and overlap readers; they are not described as an Available-board EXPLAIN proof. Full Release unit8722→integration906→host509 all pass/zero skipped. No contract/model/error-key regeneration was required by this rule; M11 separately regenerates Initial. Phone request shape is T-0855, still due in PR3. No shared DEV/device or real-mail proof is claimed here.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

2026-10-13 — Implementation verdict: **approve-with-notes**; separate security verdict: **APPROVED for the reviewed P2 security implementation**. Their own executed checks and limits are in `p2/reviewer-round1.md`, `p2/security-round1.md` and `p2/verifier-round1.md` under the evidence root. Hosted exact-head CI and guarded merge remain due; no deployment is claimed.

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.
