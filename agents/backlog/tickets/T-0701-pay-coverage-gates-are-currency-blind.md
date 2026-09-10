---
id: T-0701
title: The pay-coverage gates are currency-blind while the pay writer is currency-strict
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

**An order in a currency no cleaner has a rate in passes every gate, and then silently never gets a pay row.**

`PayCoverageLookup` and the four gates built on it — the two customer catalogue overviews, the
create-order validator and the cleaner-approval check — carry no currency term. The pay estimator and
the pay writer are both scoped to `order.CurrencyId`.

So an order in a currency with no pay configs is admitted on the strength of a rate in a different
currency, and then produces no `OrderEmployeePay` row at all. Not permanently unpayable — the unique
index already carries `CurrencyId`, both pay-config commands take one, and an admin-only
`CalculateOrderPay` can re-run the calculation once a rate exists — but the order sits silently
unpaid until somebody notices.

## Acceptance criteria

1. Every pay-coverage query filters on the order's currency.
2. An order in a currency with no cleaner rate is not offerable, by the same fail-closed rule the
   catalogue already uses for unpriced entries.
3. A test proves the gate and the writer agree: whatever the gate admits, the writer can pay.

## Notes

Verified 2026-09-10. The claim that such an order is "permanently unpayable" was refuted — the admin recalculation path exists.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
