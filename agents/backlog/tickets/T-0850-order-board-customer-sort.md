---
id: T-0850
title: Stop a cleaner sorting the order board by customer e-mail or name, which tells them which open offer belongs to a customer they know
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-12
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

## Doing (proposed, for Mike)

- The order board honours a `CustomerName` or `CustomerEmail` sort only for an administrator, as its filters already do. For anyone else, that sort entry is dropped, and the page falls back to its default order.
- A test proves it: a cleaner's two opposite customer-e-mail sorts return the same order, and an administrator's do not.

## NOT doing

- No change to the redaction or to the filters.
- No change to the native apps, which never send these fields.

## Acceptance criteria (done looks like)

- [ ] **AC1** — A cleaner's customer-name or customer-e-mail sort no longer changes the board's order. The test goes RED, then GREEN.
- [ ] **AC2** — An administrator's sort by either field still works.
- [ ] **AC3** — Security review passes (S3/S4), and the full suites pass.

## Work record

- 2026-10-12 — Filed from the native PR's security review. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
