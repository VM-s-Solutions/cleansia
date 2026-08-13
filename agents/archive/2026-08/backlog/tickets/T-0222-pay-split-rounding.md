---
id: T-0222
title: SplitPayForMultipleEmployees — currency-minor-unit split + last-share remainder reconciliation
status: done
size: S
owner: —
created: 2026-06-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 2
source: T-0125 (TC-PAY) defect scan — money-math design gap (logged, not silently fixed)
---

## Context
Found by **T-0125 (TC-PAY)** while pinning the pay math (which had zero coverage). `PayCalculator
.SplitPayForMultipleEmployees` does a **pure full-precision `decimal` divide** (`totalPay / employeeCount`)
with **no currency-minor-unit rounding and no last-share remainder reconciliation**. So an uneven split's
per-employee shares can drift a sub-cent epsilon from the total when summed — verified empirically:
`50.00 / 3` shares sum to `50.000000000000000000000000001` (over by a 27th-dp epsilon), while `100 / 3`
happens to round-trip to exactly `100` (the round-trip is **value-dependent**).

This is **correct for the calculator's current contract** (the caller owns reconciliation) and is **NOT a
blocker** — it is pinned by the characterization test
`SplitPayForMultipleEmployees_Uneven_Split_Is_A_Pure_Decimal_Divide_No_Remainder_Reconciliation`
(`src/Cleansia.Tests/Features/EmployeePayroll/PayCalculatorTests.cs`). But **before multi-employee payouts
go live**, an actual money split must guarantee `sum(shares) == total` to the currency minor unit, or one
employee is paid a cent short/long.

T-0125 was test-only and correctly **logged this rather than silently patching production** (patching to
pass a test would have hidden a money decision). This ticket is the deliberate fix.

## Acceptance criteria
- [ ] **AC1** — `SplitPayForMultipleEmployees` (or a new `SplitPayToMinorUnit`) rounds each share to the
  currency minor unit (2 dp for the current CZK/EUR set) and assigns the remainder so `sum(shares)` equals
  the input total **exactly** (last-share-takes-remainder, or largest-remainder distribution — decide and
  document).
- [ ] **AC2** — Property/exhaustive test: for a representative range of totals and employee counts (incl.
  odd cents and N that don't divide evenly), `sum(shares) == total` to the minor unit, and no share is
  negative or off by more than one minor unit from the even share.
- [ ] **AC3** — The existing characterization test from T-0125 is updated to assert the new exact-sum
  contract (replacing the "pure decimal divide, no reconciliation" pin).
- [ ] **AC4** — No regression to even splits (N divides total evenly → unchanged) or single-employee
  (whole total). The `CalculateOrderPay` pay-fan-out consumer that calls the split is reviewed to confirm it
  consumes the reconciled shares correctly.

## Out of scope
- The rest of the pay formula (already correct + pinned by T-0125).
- Multi-currency split (still CZK-centric per the platform-expandability doctrine).

## Implementation notes
- Decide minor-unit precision from the currency (the codebase is CZK-centric today; a currency param or
  the order's CurrencyId drives the dp). Document the remainder-assignment rule.
- TEST-FIRST: AC2 property test red → green.

## Status log
- 2026-06-05 — draft (created by orchestrator from the T-0125 defect scan; non-blocking, gate before
  multi-employee payouts go live).

## Review
<!-- reviewer / security / optimizer write verdicts here -->
