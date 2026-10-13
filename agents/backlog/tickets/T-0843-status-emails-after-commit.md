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
- The consumer takes tenant from the message, freshly checks the order/status, resolves language and a live guest token before sending, and makes duplicate delivery idempotent. The token is consumer-issued; it is not claimed to commit with the producer’s order change.
- **The roster.** Add one TryReturnAsync entry listing all three existing independent return paths, their reasons and what survives a caller’s rollback. Keep their behavior and commit boundaries unchanged.

## NOT doing

- Other e-mail paths. If this change finds more e-mails sent before a commit, they are reported, not moved.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A refused Start or Complete commit sends no e-mail, and its retry sends exactly one. The test goes RED then GREEN.
- [ ] **AC2** — One TryReturnAsync roster entry names all three existing independent return paths, why they commit alone, and what survives a caller rollback; their behavior is unchanged.
- [ ] **AC3** — The full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Started/Completed delivery moves to per-order/status durable outbox messages and a fresh consumer state guard. Refused commits must deliver nothing, duplicate delivery is idempotent, and a live guest link is issued by the consumer. Record all three independent TryReturnAsync credit-return paths accurately without changing their behavior. Functions must deploy with or before APIs; the approved final reset/fresh deployment avoids mixed-version messages.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 6 and 7. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
