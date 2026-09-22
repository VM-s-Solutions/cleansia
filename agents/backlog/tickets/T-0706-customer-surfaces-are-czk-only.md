---
id: T-0706
title: The customer surfaces format everything as CZK and offer no way to choose a currency
status: todo
size: M
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend, mobile]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**The booking wizard has its own module-level CZK formatters, bound about twenty times, and the catalogue DTOs carry no currency at all.**

`order-wizard.models.ts` defines `CZK_WHOLE` and `CZK_WITH_HALERE` at module level and formats every
figure in the wizard through them, ignoring the quote's own `currencyCode`. The recurring-booking
wizard passes `'CZK'` on one line while threading the real currency on the line above it. The rewards
page labels `points * 10` as CZK.

Underneath: `ServiceListItem`, `PackageListItem` and `ExtraListItem` carry a price and no currency,
and the customer host has no currency endpoint at all — so the catalogue surfaces have nothing to
read even if they wanted to. On Android the same gap is written as `formatOrderPrice(x, null)`, which
falls back to CZK by design and is documented as a known gap; on iOS it is two `"CZK"` literals.

## Acceptance criteria

1. Every customer-facing amount is labelled from the payload it came with.
2. The catalogue DTOs carry the currency they are priced in, or the customer host gains a way to know
   which currency it is being quoted in.
3. Web, Android and iOS all read it — this is a wire change, so it batches with one regeneration.

## Notes

This is the largest single surface in the programme and the one that needs a wire decision first. Cheapest correct shape is probably a currency on the catalogue response rather than a per-item field.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
