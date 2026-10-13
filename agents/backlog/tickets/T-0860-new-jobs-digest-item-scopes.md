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

- [ ] **AC1** — A stages a notice and its real employee-token/constraint commit is repeatedly refused; B has a job that starts before the next hourly tick. B persists notice/outbox/watermark now, and no A rows leak. The unfixed failure sequence is recorded RED and the isolated result GREEN.
- [ ] **AC2** — With A's fault cleared, a fresh eligible re-read sends once. One common sweep instant, child-local employee/context, two-company watermark ownership and explicit recipient tenants are verified through persisted records; disappearance stages no notice without watermark.
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

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
