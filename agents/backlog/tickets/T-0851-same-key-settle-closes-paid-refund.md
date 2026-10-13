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

- Re-read this key’s committed Succeeded status untracked **once immediately after the initial retry-ceiling read, before the first stale-row clamp/failure mutation**, in each retry entry path. Resolve a committed success to the existing refund. Exclude this key from all three later closure totals as a complementary protection. Later awaited credit and held-slice reads remain inside the interval after that status read; there is no separate fresh guard before every later mutation and no general refund-serialization claim.
- Prove actual dispute-resolution and hourly re-drive handlers plus a faithful same-context tracked wind-down preload, RED then GREEN; the preload harness does not execute the whole wind-down service. Mutation pins remove the fresh read and the three closure-key exclusions separately, including a success committed at the third Refund SUM after the fresh read.
- Then address M10: actual concurrent identical partial-refund handlers produce one provider refund and one successful audit, with the winner chosen atomically at persistence. Pin both Paid and already PartiallyRefunded orderings and refused-commit retry behavior. No Refund concurrency token.
- The immediate Work record explains the mechanism; ADR-0071’s final amendment lands with PR 6’s authorized wording sweep.

## NOT doing

- No change to the status read, which T-0844 fixed.
- The earlier N3 deferral is superseded by the approved M10 follow-up in this PR; no other refund or audit redesign.

## Acceptance criteria (done looks like)

- [x] **AC1** — A second settle of one key that finds the key already Succeeded leaves it Succeeded through actual dispute-resolution and hourly re-drive handlers and the faithful wind-down tracked-preload harness. Recorded RED/GREEN and separate fresh-read/closure-exclusion mutations prove these boundaries; the full wind-down service is not claimed executed.
  - M10: actual concurrent identical partial-refund handlers produce two Stripe method invocations with one provider idempotency key and one fake payout, one new keyed success audit and one loyalty clawback, in Paid and already-PartiallyRefunded orderings. The persistence-wait and refused-audit retry controls pass. Historical unkeyed audits remain unchanged.
- [x] **AC2** — The separate S7/S8 assessment approves the decided refund/audit mechanism with its recorded timing/history limits, and the full local Release backend unit, integration and host suites pass. Final publication gates remain separate.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Re-read this key’s committed Succeeded row untracked after the ceiling read and before every stale tracked mutation; closure totals exclude this key. The tests include wind-down’s tracked load. After this core fix, the explicitly approved M10 follow-up proves one provider refund and one successful audit for concurrent identical partial refunds at an atomic persistence boundary. No Refund concurrency token or row-repair work. ADR-0071’s final mechanism wording is sequenced with PR 6. T-0857 follows after this PR merges.

**Implementation precision, 2026-10-13:** the historical approval wording above is retained. The implemented guard is one untracked Succeeded read immediately after the initial retry ceiling and before the first stale-row mutation; the later closure sums exclude the key. It is not a separate read before every later mutation. The Work record states the remaining interval explicitly.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-13 — Filed from the security re-review of T-0844's F1 fix. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

- 2026-10-13 — Implemented T-0851, then the approved M10 follow-up, in the P2 working tree. Mike's verbatim 2026-10-13 authority remains "Analyze and Fix those" and "Go with defaults (recommended)". `RefundService` calls `GetSucceededByRefundKeyNoTrackingAsync` **once immediately after the initial retry-ceiling read**, in each retry entry path, and resolves a success before the first stale-row clamp/failure mutation. The three later closure sums exclude the current refund key. The settled-status calculation is unchanged and Refund has no concurrency token.
  - **T-0851 proof:** `p2/correctness/refund-ceiling-red-001` exited 1 with **5 failed / 5 executed** against the pre-fix behavior; `refund-ceiling-green-001` passed **18/18**, exit 0. The five race cases cover actual `ResolveDispute`, actual hourly `RedrivePendingRefunds`, a faithful same-context wind-down tracked preload, an earlier credit settlement, and a success at the later held-slice read. The tracked-preload case intentionally calls `RefundService.RedriveAsync` after loading the pending row in that same context; it does **not** run the whole `CompanyWindDownService`.
  - **Boundary correction:** the original fifth case E, including its initial RED/GREEN description, targeted the earlier pending-before SUM before the committed-success re-read, despite being described as the held-slice boundary. It is not proof of the post-read held-slice race. E now asserts that its hook fires at the **third Refund SUM**, after the fresh read, with settled credit 400/card 600. `refund-held-slice-active-green-001` passes the corrected **5/5**, exit 0, on current source and `20261013160545_Initial`.
  - **Separate mutation pairs:** suppressing only the succeeded reread gives `refund-reread-mutation-red-002`: **4 failed / 5 executed** (A–D fail; E passes), exit 1, then `refund-reread-mutation-green-001`: **5/5**, exit 0 after byte-exact restore. Removing only the three closure-key exclusions gives `refund-closure-mutation-red-002`: **1 failed / 5 executed** (corrected E commits Failed 600 over Succeeded 600; A–D pass), exit 1, then `refund-closure-mutation-green-001`: **5/5**, exit 0 after exact restore. The earlier `refund-closure-mutation-red-001` was **5/5 all green and non-refuting** because E hit the wrong SUM. The earlier reread mutation's B preservation checks passed; the actual hourly `Redriven = 1` assertion was added before red-002. Hash/original/restoration receipts are retained. These isolated mutations use their internally paired **pre-M11** `20261012190449_Initial`, not the final shared schema; they prove only the selected runtime protections.
  - **Remaining timing interval:** no Refund-row lock surrounds the T-0851 read. A settlement can commit after that read, while the retry awaits credit/held-slice reads or reaches a later positive clamp/Stripe call. The corrected E proves the zero-held closure protection, not universal serialization or a separate guard at every mutation. The smaller-positive-held-slice/provider-snapshot investigation established no new reachable loss. Existing provider lookup/idempotency and order concurrency still apply; no additional refund redesign was absorbed.
  - **M10 implementation:** after settlement and the existing loyalty flush, `IssuePartialRefund` locks the already committed Refund row through `HasPartialRefundAuditUnderLockAsync`, then checks the current company's successful `order.refund.partial` audit for `refundKey` in its typed After snapshot. Absence plus insertion stay protected through the outer pipeline audit commit; the loser uses `DeclineSuccessRow`. Existing transaction commit/rollback/cancellation/disposal releases the lock. It is not held across Stripe and does not serialize every refund operation. The snapshot keeps its previous fields and adds optional RefundKey in audit JSON, with no EF property or wire DTO change.
  - **M10 proof:** after T-0851, `partial-refund-audit-red-002` failed **2/2** actual concurrent admin/audit pipelines by producing duplicate success rows, exit 1. `partial-refund-audit-green-003` passed **6/6**, exit 0, including adjacent cancellation controls. The Paid and already-PartiallyRefunded cases each cross the pre-Stripe reads: **two Stripe method calls, one idempotency key, one fake payout, one new success audit and one loyalty clawback**. The later `audit-persistence-and-ac2-final-green-001` passes **8/8** on the current Initial, including the actual handler test that pauses the first audit absence read and observes the second PostgreSQL session waiting on `Refunds FOR UPDATE` before the first audit commits. A refused final audit commit releases the lock; replay writes one missing new audit while the independently committed refund/loyalty survive. Historical successful audits without refundKey are retained, **not backfilled or deduplicated**; replay of a pre-change refund can add a new keyed audit.
  - **Reported, not fixed:** the existing tracked-order response can report Paid while stored status is PartiallyRefunded after a concurrent identical refund. Read-only PostgreSQL probes reproduced this both at the new reread timing and at the unchanged preexisting Succeeded fast path (`p2/reviewer/status-probe`, `reviewer-status-probe-003`, `reviewer-status-legacy-fastpath-004`). Refund amount/succeeded row/new-audit correctness remain intact. This response-only residual is not absorbed into T-0851/M10 and no new ticket is filed here.
  - Exact focused commands, receipt/log hashes, counts and restoration evidence are indexed in `p2/correctness/correctness-evidence-index-final.json`, with source/behavior detail in `behavior-record-final.md`. Failed compile, fixture, stale-assembly, disk-full and Docker setup attempts are nonproof. No endpoint/DTO/client regeneration is owed. ADR-0071's final mechanism amendment remains sequenced with the authorized PR 6 wording work; T-0857 still follows after this PR merges.
- **Combined local Release validation, 2026-10-13:** after the combined Release build, the full backend suites ran from `src/` in unit → PostgreSQL integration → host order: **8,722/8,722**, **906/906**, **509/509**, all exit 0, no skipped or unexecuted cases. Receipts and TRXs are under the evidence root at `p2/local/backend-unit-full-002`, `backend-integration-full-001` and `backend-host-full-001`, with matching `.json`/`.log` files. These are local command results. Final frontend/Android/iOS and documentation gates, record refutation, exact-head CI, PR publication and merge are not asserted by this entry. No real customer email, live Stripe payout, shared DEV reset/deployment or PRO operation was used for these checks.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

Implementation assessment: **approve-with-notes**. The separate security verdict is **APPROVED for the reviewed P2 security implementation**, dated 2026-10-13. Evidence: `p2/reviewer-round1.md` and `p2/security-round1.md` under the evidence root. Their focused command results are distinct from the later full backend receipts above. Their implementation verdicts retain the limits recorded here; neither is a CI, merge or deployment result. Final records and publication gates remain outstanding.
