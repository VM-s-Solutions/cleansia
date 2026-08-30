---
id: T-0673
title: Home: informative footer, theme and language switchers, email promo capture
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
manual_steps: [nswag-regen]
sprint: 16
---

## Context
- The current footer carries almost no information. The concept adds five columns: description with store links, services, company, support, and contact with hours and registration numbers.
- The customer app has a `ThemeService` keyed on `cleansia-theme` and a `cleansia-language-switcher` component, but neither is reachable from the home page.
- The owner asked for an email capture that returns a promo code. `PromoCode` supports create and validate, but no endpoint mints-and-emails one.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [ ] **AC1** - The footer renders five columns with real links; every unknown fact is a visible placeholder, never invented.
- [ ] **AC2** - Theme and language switchers are reachable from the customer header and the footer, and both persist.
- [ ] **AC3** - The email capture renders with a consent checkbox and three states, wired to a facade.
- [ ] **AC4** - The facade is stubbed behind an interface; no fake success is shown to a user until the backend endpoint exists.

## Out of scope
- Building the backend mint-and-send endpoint - a separate backend ticket needing an owner-run NSwag regeneration.
- Marketing-consent storage beyond what `UserConsent` already models.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)

## Review
<!-- reviewer / security / optimizer write verdicts here -->
