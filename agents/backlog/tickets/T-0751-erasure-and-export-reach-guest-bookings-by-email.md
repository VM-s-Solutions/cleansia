---
id: T-0751
title: Erasure and export reach the guest bookings placed with the erased account's e-mail (Q-GDPR-01)
status: done
size: M
owner: —
created: 2026-09-15
updated: 2026-09-15
depends_on: [T-0738]
blocks: []
stories: []
adrs: [ADR-0051, ADR-0061, ADR-0062]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

Owner ruling 2026-09-15 on **Q-GDPR-01** (filed by T-0738 after A7 was found impossible by order):
*yes — reach them by e-mail*. A person who booked as a guest and later registered, or who booked as
a guest beside their account, had those orders left to the two-year order-PII sweep and their guest
audit rows keeping IP and device for three years, because nothing but the e-mail links a guest
booking to an account and the erasure blanks the e-mail.

## Doing

- **One definition of "the subject's orders":** `SubjectOrders.Of(userId, email)` in
  `Core.Domain/Orders` — the orders booked on the account **or** the orders with no `UserId` whose
  `CustomerEmail` equals the account's e-mail case-insensitively (`ToLower` both sides, the
  `LookupOrder` idiom; the column is plain text). The `UserId == null` term is load-bearing: another
  account's order carrying the subject's address in its contact field stays that account's. Asked
  with the live e-mail, before `User.Anonymize()`.
- **Past the tenant filter** (the review fix): a guest checkout is stamped with the *market's*
  operator while the erasure runs under the subject's claim — ADR-0051's written-anonymous /
  read-under-a-claim cell, bypass + re-pin, as `LookupOrder` reads a guest order. The order read, the
  photo read, the second order load and the pay-row read go through `GetQueryableIgnoringTenant()`,
  pinned by `SubjectOrders` and then by the ids that read yields; `CommitAsync` re-stamps Added rows
  only, so the anonymised rows keep their operator's stamp.
- Every order in the set goes through the per-order path the account's own orders took (photo blob +
  row, the aggregate's customer data, the address, the pay rows); the guest audit rows are blanked by
  the second sanctioned `Pseudonymise` caller, `PseudonymiseGuestRowsForOrdersAsync(orderIds)` —
  `UserId` null, `ResourceType == nameof(Order)`, `ResourceId` in the set, a tracked walk on the
  single commit. A row sharing the id under another resource type, and a stranger's booking, are
  untouched. The immutability walk expects the two repository writes.
- **A LIVE guest booking is left out of the walk, not a refusal** (the review fix reverted the first
  cut, which had widened the blocking check to `SubjectOrders`): a guest booking has no cancel path
  (`CancelOrder` refuses `order.UserId != userId`; nothing anonymous cancels), so the subject — and
  `AdminDeleteUserAccount` through the same `FindRefusalAsync` — would have been dead-ended on an order
  the account does not list, over what may be a stranger's typo. The blocking rule was never part of
  the ruling (T-0738 NOT). The walk reads `SubjectOrders` minus `ErasureBlockingStatuses`; the
  residual is stated on `ErasureBlockingStatuses`: its contact data stays until the job ends and the
  order-PII sweep reaches it. → Q-GDPR-03, T-0753.
- **The export's orders section** reads the same set the same way. **Its trail stays the account's
  own rows**: the guest predicate would disclose the IP and device of whoever placed a booking under
  the subject's address — a stranger's, when it is a typo — which the confirmation e-mail never carried.
  `GdprExportOrderDto`'s doc claims only what the code does.

## NOT

No change to the blocking rules (the account's own live orders). No guest rows in the export's trail.
No change to the incident file's order set (the account's and the proven ones — stated on the
service). No schema change.

## Status log

- 2026-09-15 — shipped in `6ab64fff` (`SubjectOrdersTests` — the four corners of the predicate;
  `ErasureBlockingOrderStatusTests`; `GuestOrderErasureTests` on Postgres; `ErasureSingleCommitTests`
  carries a guest order and its row through the poisoned and the landed commit; Tests 5451/5451;
  the Postgres suites could not run — Docker Desktop down, not started by an agent — so the walk was
  driven over SQLite with the real service and repositories in an uncommitted probe) and `ca6dc84e`
  (review: the blocking check restored to the account's own orders and the live guest booking left
  out; the four guest reads past the tenant filter; `GuestOrderErasureTests` seeds the guest booking,
  its address, photo and audit row under the SECOND operator and asserts the stamp survives, adds a
  live guest booking that keeps everything while the erasure completes, and the export lists own +
  guest (other market) + live guest with `orderCount 3` and not the stranger's; Tests 5452/5452; the
  Postgres proof still owed). The owed Postgres proof landed with T-0752's run (`Features.Gdpr`
  38/38 on Postgres, `9c0b9801`). Recorded in ADR-0062 D5 as amended 2026-09-15;
  `/flows/gdpr-and-audit`; business rules `#customer-record`; the model's `Order` row.
