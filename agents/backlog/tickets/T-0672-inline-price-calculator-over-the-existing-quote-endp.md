---
id: T-0672
title: Home: inline price calculator over the existing quote endpoint
status: ready
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-30
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
- [ ] **AC1** - Selecting a service, a size and a slot returns a real quote from `QuoteOrder`; the price is never computed client-side.
- [ ] **AC2** - The component delegates all logic to a facade holding state in signals; the component is presentational and `OnPush`.
- [ ] **AC3** - Three explicit data states (loading, error, loaded) are rendered.
- [ ] **AC4** - Continuing carries the selection into the order wizard without re-entry.
- [ ] **AC5** - Size options come from a configurable source, not a hardcoded Czech list - see T-0675.

## Out of scope
- Any change to `QuoteOrder` or its DTO.
- Persisting a draft order for anonymous visitors.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)

## Review
<!-- reviewer / security / optimizer write verdicts here -->
