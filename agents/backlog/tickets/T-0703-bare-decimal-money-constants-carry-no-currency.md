---
id: T-0703
title: Money constants and thresholds are bare decimals with no currency
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

**Six business rules are expressed as numbers that only mean anything in crowns.**

- `BookingPolicy.NoShowCreditCzk` — the apology credit, guarded only by "order currency == platform
  default", so it silently pays nothing on any other currency.
- `LoyaltyTierConfig.MinimumOrderAmountForDiscount` — seeded 1000, compared against an order total in
  whatever currency the order is.
- `PromoCode.MinimumOrderAmount` — the fixed-amount discount already carries a `CurrencyId`; the
  minimum does not.
- The loyalty earn rate is a bare `/10m` divisor, so the same real spend earns ~25x fewer points in
  EUR.
- `IssueCustomerCredit`'s 10 000 sanity cap.
- `RefundStripeFixedFee`, a per-country decimal deducted from a refund denominated in the order's
  currency.

## Acceptance criteria

1. Each constant either becomes per-currency or is documented as deliberately single-currency with
   the reason.
2. `check-booking-policy-parity.mjs` still passes, or is extended if the no-show credit stops being a
   scalar.

## Notes

The plan of record deferred the loyalty divisor and per-currency no-show credit as additive. This ticket collects them so the set is visible in one place.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
