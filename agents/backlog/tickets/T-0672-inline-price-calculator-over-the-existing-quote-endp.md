---
id: T-0672
title: Home: inline price calculator over the existing quote endpoint
status: done
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-31
depends_on: ['T-0671']
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- The concept puts a live calculator in the hero. `QuoteOrder` already exists and the generated customer client already exposes `orderClient.quote(...)` - the order wizard calls it today, so no NSwag regeneration is needed.
- Research on service marketplaces is consistent that a booking tool in the hero outperforms a promise in the hero.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [x] **AC1** - Selecting a service, a size and a slot returns a real quote from `QuoteOrder`; the price is never computed client-side.
- [x] **AC2** - The component delegates all logic to a facade holding state in signals; the component is presentational and `OnPush`.
- [x] **AC3** - Three explicit data states (loading, error, loaded) are rendered.
- [x] **AC4** - Continuing carries the selection into the order wizard without re-entry.
- [x] **AC5** - Size options come from a configurable source, not a hardcoded Czech list - see T-0675.

## Out of scope
- Any change to `QuoteOrder` or its DTO.
- Persisting a draft order for anonymous visitors.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)
- 2026-08-31 - ground-truthed: PARTIAL, not shipped. AC2, AC3 and AC5 hold.
  - AC1 half-holds: the price is a genuine `QuoteOrder` pass-through with no client-side arithmetic,
    but there is no SLOT - the calculator offers service and size only, and `cleaningDate` is never
    set even though the DTO carries it.
  - AC4 FAILS outright: `quick-quote.component.html:55` is a bare `routerLink="/order"` with no
    queryParams and no shared store, so the visitor re-enters both selections. The pattern exists
    unused two files away (`services.component.html:12` passes `serviceId`), and the wizard reads
    `serviceId` at `order-wizard.component.ts:187` - but it has no reader for rooms/bathrooms at all,
    so carrying the size needs a wizard change too, not just a query param.
- 2026-08-31 - done. Both failing ACs are closed.

  **AC4 - the handoff was missing entirely.** Continue was a bare
  `routerLink="/order"` with no query params and no shared store, so a visitor who picked a service
  and a size and watched a price appear re-entered both. It now carries `serviceId`, `rooms`,
  `bathrooms` and `cleaningDate`, and the wizard reads all four - it previously read `serviceId`
  alone and had no reader for the size at all, so this needed both ends. The wizard clamps what it
  reads (integers, 0-20) rather than trusting them: they arrive from a URL.

  **AC1 - the missing slot was not cosmetic.** `QuoteOrder` charges an express surcharge for a
  cleaning booked soon (`OrderPricingCalculator` takes `CleaningDate`), so a calculator with no date
  could show a number that disagreed with the number at checkout - the one thing a price calculator
  must not do. An optional date field now feeds `cleaningDate` into the same call. Optional, because
  without one the base price is a truthful quote; the hint says what booking soon costs.

  `date_label` and `date_hint` are seeded in all five locales; leaf parity re-verified at 1489 keys
  each, identical sets.
## Review
<!-- reviewer / security / optimizer write verdicts here -->
