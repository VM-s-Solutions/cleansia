---
id: T-0855
title: Use available-seat filters on both partner phone boards
size: S
owner: pm
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0854]
blocks: []
stories: []
adrs: ["0037"]
layers: [android, ios, docs]
security_touching: true
---

# Use available-seat filters on both partner phone boards

## Context

ADR-0037 §D9 records the 2026-08-03 Available-tab switch (Q-AVAIL-01), but the current phone query shapes retain isUnassigned. Decision 4 explicitly schedules that existing ruling with mike-5. Both boards must show a free second seat and an eligible cover seat while excluding the current cleaner. The P2 contact report maps the backend rule but does not freshly verify this native implementation.

## Owner decision — 2026-10-13

Mike, answer 5: "I don't think that it needs to be offerable".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This implements default decision 4, shipping ADR-0037 D9 with the slot-end change. The older ruling is cited, not re-quoted from memory.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/contact-verifier-before.md`

## Doing

- Ground-truth both partner Available query builders at the native phase head. Replace isUnassigned with hasAvailableSpots plus excludeEmployeeId on Android and iOS.
- Preserve existing statuses, paging, sorting and account/hold/currency/contract restrictions; use the existing client parameters.
- Pin each encoded wire request and exercise a half-crewed job, cover seat and current-employee exclusion under the slot-end backend rule. Record fulfillment of ADR-0037 D9 without changing its historical body.

## NOT doing

- No backend endpoint/rule/DTO change or generated client/spec regeneration.
- No new filters, dashboard behavior, translation or crew/cover lifecycle redesign.
- No backend implementation in PR 2 for this phone switch.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [ ] **AC1** — Each Available request sends hasAvailableSpots and the signed-in employee exclusion, without isUnassigned. Wire tests demonstrate the old request shape RED and the new shape GREEN.
- [ ] **AC2** — Both apps show an eligible free second seat and cover seat, exclude an already-assigned current cleaner, and preserve no offer at/after slot end. Record command/device evidence honestly and distinguish their scope.
- [ ] **AC3** — Both platforms full relevant suites and parity/symbol gates pass on the actual head; reviewer/security checks preserve current-account and hold restrictions; ADR-D9 fulfillment and owner ruling are recorded.

## Dependencies and execution

Phase: **PR 3**. Dependencies: T-0854. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Existing partner Orders list query/wire tests on both native platforms.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
