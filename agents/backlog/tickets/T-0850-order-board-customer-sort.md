---
id: T-0850
title: Stop a cleaner sorting the order board by customer e-mail or name, which tells them which open offer belongs to a customer they know
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
---

## Context

Found on 2026-10-12 by the security review of the native follow-ups (T-0832, T-0835, T-0836). It is low and pre-existing; that branch's apps never send these fields. The review is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/impl/security_native.json`.

- **The two fields are protected everywhere but the sort.**
  - As filters, `CustomerName` and `CustomerEmail` reach the query only for an administrator (`GetPagedOrders.cs:96-97`).
  - On the board, a browsing cleaner sees both blanked (`OrderPiiRedaction.cs:16-25`).
  - But `OrderSort` accepts both as sort keys (`OrderSort.cs:22-29`), and `GetPagedOrders` applies the request's sort for every caller (`:120-121`).
- **How a cleaner uses it.**
  - They send `GET /api/Order/GetPaged?Filter.CleaningDateFrom=2020-01-01&Sort[0].Field=customerEmail&Sort[0].Direction=0`, then the same request with `Direction=1`.
  - Their own past jobs come back with the customer's e-mail visible, and open offers come back blanked.
  - Ties are broken by `Id` (`SortMapper.cs:14`, `BaseRepository.cs:88-94`), so equal e-mails keep their relative order in both calls, while different ones swap places.
  - So an open offer that never swaps places with a known customer's job belongs to that customer.
- **Who can do it:** any signed-in cleaner, from a hand-written request.

## Doing — approved 2026-10-13

- For a non-admin open-board read (`!isAdmin && employeeIdFilter is null`), drop every `CustomerName`/`CustomerEmail` sort entry before ForOrderPage, using resolver-equivalent normalization. Preserve other safe entries; if none remain, the normal default applies. Administrator and cleaner own-order sorting stay unchanged.
- A test proves it: a cleaner's two opposite customer-e-mail sorts return the same order, and an administrator's do not.

## NOT doing

- No change to the redaction or to the filters.
- No change to the native apps, which never send these fields.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A cleaner's customer-name or customer-e-mail sort no longer changes the board's order. The test goes RED, then GREEN.
- [ ] **AC2** — An administrator's sort by either field still works.
- [ ] **AC3** — Security review passes (S3/S4), and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Mike, answer 4: "I think that not since it's using the account's by default".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Drop every customerName/customerEmail sort entry only for a non-admin open-board read (!isAdmin && employeeIdFilter is null), using the resolver’s normalization before ForOrderPage. Administrator and cleaner own-order sorting stay. Record the own-order redacted-PII sort residual in docs/api/orders.md and the separate security verdict. T-0853 resolves answer 4’s account e-mail question in the same PR.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the native PR's security review. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-13 — The security review of the native branch's later commits found this wider than filed (`p5/impl2/security_native_followups.md`, its F1, off-tree in the Wave F folder), and proposes Medium in place of Low:
  - A cleaner needs no past customer: a signed-in booking stamps whatever contact e-mail and name the request sends (`CreateOrder.cs:1111-1112`), so a cleaner can book one cash visit under a guessed e-mail and use it as the reference order.
  - `GET /api/Dashboard/GetUpcomingOrders` on both partner hosts reaches the same handler, and every sort entry is applied, not only the first (`BaseRepository.cs:88-94`). So the fix belongs in `GetPagedOrders` (`:120-121`), dropping both fields from every entry.
  - A question for Mike (S1): should a signed-in booking be able to name a contact e-mail other than the account's? The order's status e-mails go to it (`TakeOrder.cs:484-485`).

## Review

Pending.
