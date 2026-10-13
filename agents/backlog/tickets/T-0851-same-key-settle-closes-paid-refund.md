---
id: T-0851
title: Stop a second settle of one refund key closing the refund Stripe paid as Failed when that refund took everything left on the card
size: S
owner: pm
created: 2026-10-13
updated: 2026-10-13
depends_on: [T-0844]
blocks: []
stories: []
adrs: [0071]
layers: [backend]
security_touching: true
---

## Context

Found on 2026-10-13 by the security re-review of T-0844's F1 fix, its N1, rated Medium under S7. It is pre-existing: the same probe reproduces it at master `6952149`. The review is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/impl2/security_rereview.md`, with its logs and probe under `p5/sec-rereview/`. Lines are as of `1768d58`.

- **What happens.** Two settles of one refund key K race.
  - The later settle loads K's row while it is still Pending, and the earlier settle then commits K as Succeeded.
  - The later settle's ceiling read counts every Succeeded refund on the order, so it counts K's own row as another refund (`RefundService.cs:83-86`, through `CardRefundedOrOwedBeforeRetryAsync` at `:267-271`; the re-drive's `:207-212`).
  - T-0844's F1 fix took K out of the status read only (`:377-378`).
- **When K took everything left on the card**, the later settle finds nothing refundable and closes K as Failed: `existing.MarkFailed()` (`:103-111`), or `NothingLeftToRedrive` (`:279-293`). Stripe paid K; the row says Failed.
- **Who commits it:**
  - a dispute's resolution, whose nothing-refundable continuation goes on to resolve the dispute (`ResolveDispute.cs:181-189`);
  - the hourly re-drive (`RedrivePendingRefunds.cs:206`);
  - the company wind-down (`CompanyWindDownService.cs:398`).

  An administrator's partial or full refund returns a failure, so its commit rolls back. A cancel on a request path is refused by the order's concurrency token.
- **Triggers:**
  - an administrator's dispute resolution for the whole remaining amount, sent twice at once;
  - the company wind-down's re-drive of a wound-down order's refund beside the hourly re-drive, which treats that key as a cancellation's own (`RedrivePendingRefunds.cs:111-113`);
  - two deliveries of the wind-down message overlapping.
- **Consequences.** The money moved once, and Stripe pays nothing twice. But:
  - the Refunds row says Failed, against "a recorded Succeeded row always means money actually moved" (`Refund.cs:92-95`);
  - the revenue report subtracts only Succeeded refunds, so it overstates that order's revenue (`GetRevenueReport.cs:65`);
  - the review adds that the order's loyalty and payroll reads undercount;
  - no alert is raised.
- **Evidence.** The review's scratch probe, `ProbeSameKeyCeilingReadTests` (`p5/sec-rereview/probe/src/Cleansia.IntegrationTests/Features/Refunds/`), drives the real dispute-resolution and re-drive handlers on PostgreSQL. It lets the other settle commit just before the first refund sum.
  - Its two tests fail at `1768d58` and at `6952149` (`probe-run.txt`, `probe-base-run.txt`).
  - Each ends with Stripe's 1 000 paid on the key, the refund row Failed and the order Refunded.

**If not built:** in a rare race, a refund Stripe paid is recorded as Failed. The money is right, but the books are not, and nobody is told.

## Doing — approved 2026-10-13

- Re-read this key’s committed status untracked after the ceiling read and before every mutation of the stale tracked row. Resolve a Succeeded row to the existing refund. Exclude this key from the closure totals as well; these are complementary requirements.
- Prove dispute, hourly re-drive and wind-down orderings, including wind-down’s tracked load, RED then GREEN; mutation pins remove the fresh read and key exclusion separately.
- Then address M10: actual concurrent identical partial-refund handlers produce one provider refund and one successful audit, with the winner chosen atomically at persistence. Pin both Paid and already PartiallyRefunded orderings and refused-commit retry behavior. No Refund concurrency token.
- The immediate Work record explains the mechanism; ADR-0071’s final amendment lands with PR 6’s authorized wording sweep.

## NOT doing

- No change to the status read, which T-0844 fixed.
- The earlier N3 deferral is superseded by the approved M10 follow-up in this PR; no other refund or audit redesign.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A second settle of one key that finds the key already Succeeded leaves it Succeeded, through a dispute's resolution, the hourly re-drive and the wind-down. The tests go RED, then GREEN.
- [ ] **AC2** — Security review passes (S7), and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Re-read this key’s committed Succeeded row untracked after the ceiling read and before every stale tracked mutation; closure totals exclude this key. The tests include wind-down’s tracked load. After this core fix, the explicitly approved M10 follow-up proves one provider refund and one successful audit for concurrent identical partial refunds at an atomic persistence boundary. No Refund concurrency token or row-repair work. ADR-0071’s final mechanism wording is sequenced with PR 6. T-0857 follows after this PR merges.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-13 — Filed from the security re-review of T-0844's F1 fix. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
