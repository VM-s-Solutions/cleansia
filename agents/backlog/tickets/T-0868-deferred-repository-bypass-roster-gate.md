---
id: T-0868
title: File the deferred repository-wide bypass roster gate
size: M
owner: pm
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0837]
blocks: []
stories: []
adrs: ["0051", "0074"]
layers: [backend, docs]
security_touching: true
---

# File the deferred repository-wide bypass roster gate

## Context

T-0837 requires filing and citing the absent repository-wide gate instead of claiming T-0606 exists. ADR-0051 remains proposed; ADR-0074 rejected building a roster as its naming fix. The fresh data report confirms 34 unswept direct-bypass files and helper/public-wrapper paths, and explicitly requires T-0868 filing only. This ticket preserves the future gate concept; no gate implementation is authorized in this wave.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Answer 8 approves T-0837. Its governing verifier correction says to file the roster gate as its own ticket, not build it. The default-choice quote does not claim acceptance of the proposed ADR.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/data-verifier-before.md`

## Doing

- Create this follow-up record and one INDEX row, tied to T-0837’s grounded naming roster and ADR-0051/ADR-0074 authority.
- Have T-0837 replace S8’s absent T-0606 gate citation with T-0868, visibly identified as deferred rather than a current CI gate. The specifically approved ADR-0051 dated head pointer preserves its historical citations.
- Record the future gate’s question: a source roster would attribute real bypass calls through methods/helpers/wrappers and detect an unaccounted bypass. Its design/evidence and any implementation are left for separately scheduled work.

## NOT doing

- FILE ONLY: no scanner/test/checker/CI workflow, no roster build, no probe mutation or passing-gate claim.
- No accepted old ADR body rewrite, ADR-0051 acceptance claim or independent design approval fabricated from this filing.
- No T-0837 naming implementation, query behavior/auth/tenancy change or new abstraction here.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — This ticket file and exactly one INDEX row exist with the correct ID and T-0837 dependency; no duplicate ticket-state field/log copy is introduced.
- [x] **AC2** — S8 cites T-0868 for the deferred future roster, no longer describes absent T-0606 as an existing gate, and the targeted ADR-0051 head pointer preserves its historical body/citations. Text checks show no false current gate/accepted-ADR claim.
- [x] **AC3** — The Work record clearly distinguishes completed filing/citation from deferred gate construction. No roster test/scanner/CI implementation appears in the filing diff; the future design question is recorded without inventing an owner blocker.

## Dependencies and execution

Phase: **PR 2 filing only**. Dependencies: T-0837. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Backlog follow-up and deferred guidance citation; future source roster design unscheduled.

Filing verification is a text/record check, not behavioral RED/GREEN. This wave runs no deferred roster mutation or suite for a gate that does not exist.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

- 2026-10-13 — Filing/citation action complete: exactly one ticket and INDEX row with T-0837 dependency. S8 visibly cites T-0868 as deferred future work; ADR-0051 has only the approved dated head pointer and retains its proposal/body/historical citations. Text/body verification and independent filing round2 are CLEAN (`filing/refuter-round2.md`, `p2/verifier-round1.md`). No scanner/test/workflow or gate implementation exists in this change. The row remains draft for separately scheduled construction; completion here means only filing/citation, with the future design question retained above.

## Review

2026-10-13 — Filing/citation verified; **CLEAN. No confirmed material residuals.** Deferred construction is not an owner blocker or an accepted ADR claim.
