---
id: T-0702
title: Reports and roll-ups add amounts in different currencies into one number
status: todo
size: M
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**Every total on the admin revenue report, the payroll report, partner earnings and pay-period aggregation is a sum over mixed units the moment a second currency exists.**

`GetRevenueReport` sums `TotalPrice` over every order in the window with no currency grouping, in
seven places, and the UI labels the result CZK. The payroll report does the same with `TotalAmount`.
Partner earnings totals and pay-period aggregation both sum across currencies and stamp one code.
`ExpireStaleCredit` sums `TotalExpired` into one unlabelled decimal.

None of these is wrong today, because there is one currency. All of them become quietly wrong — not
broken, wrong — on the day there are two, and a wrong revenue number is the kind that gets believed.

## Acceptance criteria

1. Every money aggregate is either grouped by currency or explicitly scoped to one.
2. No screen renders a total whose currency it cannot name.
3. The revenue report additionally excludes cancelled orders and subtracts refunds (pre-existing
   defects noted in the plan of record).

## Notes

Also covers the order list sorting numerically by totalPrice across currencies, and the missing currency filters on orders and invoices.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
