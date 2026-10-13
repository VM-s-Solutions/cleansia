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

- [ ] **AC1** — Fixed-clock SQL/C# tests assert explicit expected ids before start, after start/before end, at end and after end for New cash and started Paid work; exactly at end is false. The end case is RED before implementation and GREEN after.
- [ ] **AC2** — Real PostgreSQL agreement/surface tests show a 60-minute unfilled slot at +10 minutes remains offered/browsable/takeable, and a started 180/240-minute slot at +130 minutes remains on board, dashboard count/preview and browse/take until its raw end. This proves the old two-hour floors do not pre-empt the rule.
- [ ] **AC3** — At/after end, a stale open-list Take adds no assignment and returns the existing refusal; seat-open notifier has zero recipients/candidate query. Existing assigned cleaner, owner customer and administrator access to ended rows remains. Holds/seat/currency/money controls stay intact.
- [ ] **AC4** — Existing OrderStatusSetPredicatePlanTests remains green with its actual completed-dashboard/overlap scope accurately described; real availability agreement tests cover the changed board rule. New ADR, dated pointer, living pages and separate security/money review record the decided boundary and preserved late-fill behavior.

## Dependencies and execution

Phase: **PR 2**. Dependencies: T-0850. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Single OrderAvailability rule, SQL/scalar agreement and one captured time per consumer.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
