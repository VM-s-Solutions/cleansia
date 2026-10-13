---
id: T-0843
title: Send the Started and Completed e-mails only once their commit lands, and put the remaining self-committing credit returns on the roster
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0844]
blocks: []
stories: []
adrs: [0038]
layers: [backend, docs]
security_touching: true
---

## Context

The 2026-10-12 design ruling on BE-11 to BE-14 reported these as items 6 and 7. Item 7 came from that ruling's security review. Both are off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/`, files `architect_be-lead.json` and `security_be.json`. Lines are as of `207d2f319`.

**The status e-mails go out before the commit.**
- `StartOrder.cs:200-202` and `CompleteOrder.cs:302-304` send the status e-mail through SendGrid, synchronously, inside the handler (`EmailService.cs:1222`). The unit-of-work pipeline commits only after the handler returns.
- For a guest, each handler also mints an access token (`guestAccessTokenIssuer.IssueForGuest(order)`). The same commit saves it.
- If that commit is refused, the e-mail has already gone, with a link that does not work. A retry then sends a second e-mail (S7).
- `CompleteOrder` takes the loyalty and referral locks after the e-mail (`:311`, `:313`), so a refusal there also comes after the send.
- T-0844's `PaymentStatus` token adds one more way for these commits to be refused.
- A shape that waits for the commit already exists: the guest cancellation e-mail is a tracked row that commits with its order (`GuestCancellationEmail.cs:30-36`).

**Three credit returns still commit on their own.** After T-0844, the return made when an order ends joins the commit that ends the order. But `TryReturnAsync` still commits by itself when no transaction is open (`CreditAccountRepository.cs:107-110`). Three paths use that branch:
- `CreateOrder`'s compensation (`CreateOrder.cs:1185`);
- a refund's credit share (`RefundService.cs:372` → `:604-607`);
- a pending refund's credit leg: `CreditUnwind.ReturnPendingRefundCreditLegAsync` (`CreditUnwind.cs:73-89`), called from `CustomerOrderCancellation.cs:194`, `PlatformOrderCancellation.cs:131` and `CleanerNoShowCancellation.cs:73`.

None of the three is on the self-committing roster (`consistency.md:459-501`, families A and B, under ADR-0038). Each may be right to commit alone, and the roster is where that case is made.

**If not built:**
- when one of these commits is refused, the customer gets a duplicate e-mail, or one with a dead link. This is rare;
- a later change could break one of the three self-commits without knowing it was deliberate.

## Doing — approved 2026-10-13

- **The e-mail intent rides the commit.** Start and Complete stage one durable outbox message per order/status with the order change. A refused commit stages no durable intent and sends no e-mail; a retry commits one intent.
- The consumer takes tenant from the message envelope, freshly checks the committed order/status, resolves language and commits a live guest token before sending. The existing send-then-record guard sends once on sequential redelivery; an external send followed by a lost claim, or concurrent sends, can still duplicate email. The consumer-issued token has its own commit and is not part of the producer’s order commit.
- **The roster.** Add one TryReturnAsync entry listing all three existing independent return paths, their reasons and what survives a caller’s rollback. Keep their behavior and commit boundaries unchanged.

## NOT doing

- Other e-mail paths. If this change finds more e-mails sent before a commit, they are reported, not moved.

## Acceptance criteria (done looks like)

- [x] **AC1** — A refused Start or Complete commit sends no e-mail and persists no durable intent; its retry persists exactly one per-order/status intent. The consumer commits a live guest credential before sending and sends once under sequential redelivery. Producer RED/GREEN and consumer controls pass.
  - Delivery retains the existing send-then-record limit: concurrent sends or a crash after the external send but before the processed claim can duplicate email. This criterion does not assert exactly-once external delivery.
- [x] **AC2** — One `TryReturnIgnoringTenantAsync` roster entry names all three existing independent return paths, why they commit alone, the frozen-books guard and the balance/ledger that survive a caller rollback; their behavior is unchanged. The explicit tenant-bypass naming comes from T-0837.
- [x] **AC3** — The full local Release backend unit, integration and host suites pass. Final publication and Functions/API deployment ordering remain separate obligations.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Started/Completed delivery moves to per-order/status durable outbox messages and a fresh consumer state guard. Refused commits must deliver nothing, duplicate delivery is idempotent, and a live guest link is issued by the consumer. Record all three independent TryReturnAsync credit-return paths accurately without changing their behavior. Functions must deploy with or before APIs; the approved final reset/fresh deployment avoids mixed-version messages.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 6 and 7. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

- 2026-10-13 — Implemented the approved Started/Completed email commit boundary and credit roster record in the P2 working tree. Mike's verbatim 2026-10-13 authority, "Analyze and Fix those" and "Go with defaults (recommended)", remains above.
  - `StartOrder` and `CompleteOrder` now enqueue `SendOrderStatusEmailMessage`, keyed per order/status, in the same UnitOfWork commit as the status change. A refused producer commit leaves no durable email intent or guest token and makes no send. A retry commits one intent. `SendEmailHandler` adopts the envelope's operator, reads the committed current order/status untracked under that operator, and rejects missing/stale/anonymized destinations. It sends to stored `order.CustomerEmail` using the approved booking → account → message language precedence and locale resolver.
  - A guest credential is issued **by the consumer and committed independently before the external send**; it is not asserted to commit with the original order change. Both Started and Completed consumer controls inspect a committed, unrevoked, unexpired token from a separate context during the send. The queue body contains no raw guest credential. The existing send-then-record delivery guard sends once on sequential redelivery. A crash after send/before the processed claim, a lost claim or concurrent sends can duplicate external email; retry can leave additional guest credentials. No exactly-once external-delivery claim is made.
  - **RED/GREEN:** `p2/correctness/status-email-red-003` exited 1 with **2 failed / 2 executed**, proving that the original Start/Complete handlers sent before refused commits. `status-email-producer-green-001` passed **16/16**, exit 0, covering producer refusal/retry, live guest links, language and stale-message/sequential-redelivery controls. Later correctness groups pass **207/207** unit and **31/31** PostgreSQL, exit 0. Exact commands, timestamps, receipt/log hashes and counts are in `p2/correctness/correctness-evidence-index-final.json`; source/behavior detail is in `behavior-record-final.md`. Earlier compile, invalid fixture and stale-assembly attempts are nonproof.
  - The production-invocation guard in `GuestTrackLinkTests` still requires at least four actual calls supplying the guest-token argument, with an explicit Functions-consumer pin. Its old file-count guard failed in `p2/local/backend-unit-full-001` when three calls moved into the same consumer file; it was corrected to scan invocations without weakening the four-call minimum. `guest-track-source-guard-green-001` passes **1/1** Release; the final full unit result below also includes it.
  - **Credit roster, unchanged behavior:** `agents/knowledge/consistency.md` has one independent-return entry for `CreditAccountRepository.TryReturnIgnoringTenantAsync`, naming failed-checkout compensation, the confirmed-refund credit share and the pending-refund credit leg. It states why each must survive a later caller failure, which committed balance/ledger survives that failure, and the frozen-company account guard. The approved T-0837 names propagate the explicit tenant-bypass suffix, including credit `EnsureForUserIgnoringTenantAsync`; no credit-return transaction boundary or behavior changes here.
  - **Deployment:** the Functions consumer must deploy **with or before** APIs that publish the new order-status discriminator. The approved coordinated final deployment remains the release procedure; no mixed-version or real-mail deployment result is claimed by the local checks. No DTO/endpoint/schema or client/spec regeneration is owed by this message change.
- **Combined local Release validation, 2026-10-13:** after the combined Release build, the full backend suites ran from `src/` in unit → PostgreSQL integration → host order: **8,722/8,722**, **906/906**, **509/509**, all exit 0, no skipped or unexecuted cases. Receipts and TRXs are under the evidence root at `p2/local/backend-unit-full-002`, `backend-integration-full-001` and `backend-host-full-001`, with matching `.json`/`.log` files. These are local command results. Final frontend/Android/iOS and documentation gates, record refutation, exact-head CI, PR publication and merge are not asserted by this entry. No real customer email, live Stripe payout, shared DEV reset/deployment or PRO operation was used for these checks.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

Implementation assessment: **approve-with-notes**. The separate security verdict is **APPROVED for the reviewed P2 security implementation**, dated 2026-10-13. Evidence: `p2/reviewer-round1.md` and `p2/security-round1.md` under the evidence root. Their focused command results are distinct from the later full backend receipts above. Their implementation verdicts retain the limits recorded here; neither is a CI, merge or deployment result. Final records and publication gates remain outstanding.
