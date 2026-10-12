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

## Doing (proposed, for Mike)

- In the retry path, treat this key's committed row as this refund. The review offers two ways:
  - re-read its status untracked, and resolve to the existing refund when it is Succeeded; or
  - leave the key out of the ceiling and closure reads, as F1's fix did for the status read.

  The implementation chooses, and ADR-0071 records why.
- The probe's two cases become the pins, RED first and then GREEN.

## NOT doing

- No change to the status read, which T-0844 fixed.
- Two concurrent partial refunds that each write an audit row (the re-review's N3) stay as T-0844 reports them.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A second settle of one key that finds the key already Succeeded leaves it Succeeded, through a dispute's resolution, the hourly re-drive and the wind-down. The tests go RED, then GREEN.
- [ ] **AC2** — Security review passes (S7), and the full suites pass.

## Work record

- 2026-10-13 — Filed from the security re-review of T-0844's F1 fix. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
