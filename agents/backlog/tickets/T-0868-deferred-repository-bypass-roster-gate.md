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

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.

- 2026-10-13 — Filing/citation is merged. INDEX retains draft for separately scheduled future gate construction; no roster scanner or CI gate was built.
