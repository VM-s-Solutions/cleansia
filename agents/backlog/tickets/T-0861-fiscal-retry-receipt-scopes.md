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

- [ ] **AC1** — With real separate contexts for receipts A/B/C, B's fiscal-state commit refuses after staged fiscal work; A/C durably record only their permitted results, with no B rows carried into C. Repeat B fault. Prove actual unfixed RED or explicitly characterize a non-RED sequence and add a scope-ownership pin tied to it.
- [ ] **AC2** — Fault B's actual second email-claim commit: no B email is sent before its durable claim and C still completes its own claim/send. Fault third metadata commit: B's already durable claim prevents second email while C remains healthy. Do not claim B is selected on the next tick after fiscal state succeeded.
- [ ] **AC3** — Two-company child-local provider/config/order ownership, fee receipts, fiscal enforcement matrix, stable registration token and host cancellation remain. Full suites, S7/S8 review and catalog removal have evidence. The separately reported fiscal email-retry predicate/test-label finding is excluded from this ticket's fix/AC.

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

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
