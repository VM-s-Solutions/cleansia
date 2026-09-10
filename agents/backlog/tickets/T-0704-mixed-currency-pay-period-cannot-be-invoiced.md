---
id: T-0704
title: A pay period holding two currencies can never be invoiced, and the period can still close
status: todo
size: M
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**The payout-invoice currency is derived from the pay rows being invoiced and fails if they disagree — with no recovery path.**

A cleaner who worked a CZK job and a EUR job in the same period has `OrderEmployeePay` rows in two
currencies. The invoice generator derives one currency from those rows and cannot, so no invoice is
produced — ever — while the period itself can still be closed around them.

There is no split-by-currency path and no way for an admin to resolve it.

## Acceptance criteria

1. A period containing pay in N currencies produces N invoices, or is refused before it can close.
2. Whichever is chosen, an admin can see the state and act on it.
3. An integration test covers a two-currency period end to end.

## Notes

Also: the per-job pay DTO carries no currency at all on the partner contract, so a cleaner cannot see what a job pays in.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
