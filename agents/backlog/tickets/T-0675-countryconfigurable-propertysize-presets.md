---
id: T-0675
title: Country-configurable property-size presets
status: ready
size: L
owner: architect
created: 2026-08-30
updated: 2026-08-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, db, backend, frontend, docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- The booking calculator offers `1+kk ... 4+kk, Dum`, which is Czech-specific naming. The owner asked that expansion to other countries be configurable rather than hardcoded.
- The domain is already country-neutral: `Order` stores `Rooms` and `Bathrooms` as ints and `OrderPricingCalculator` computes `BasePrice + PerRoomPrice x (rooms + bathrooms)`. Nothing persists `3+kk`.
- `CountryConfiguration` already holds per-country presentation config (`TaxIdLabel`, `RegistrationNumberFormat`, `LegalRequirementsJson`), so it is the natural home.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [ ] **AC1** - An ADR records the decision and why the domain does not change.
- [ ] **AC2** - A `PropertySizePreset` catalogue exists keyed on country, carrying `Code`, `SortOrder`, `Rooms`, `Bathrooms`, an owned per-language `Translation` dictionary and `IsActive`.
- [ ] **AC3** - CZ and SK are seeded with their existing options; labels come from the catalogue, not a hardcoded list.
- [ ] **AC4** - Orders continue to persist ints, so historic pricing stays reproducible when a preset is retired.
- [ ] **AC5** - Retiring a preset never breaks an existing order.

## Out of scope
- Launching any new country.
- Changing `OrderPricingCalculator`.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)

## Review
<!-- reviewer / security / optimizer write verdicts here -->
