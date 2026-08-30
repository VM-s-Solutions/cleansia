---
id: T-0675
title: Country-configurable property-size presets
status: blocked
size: L
owner: architect
created: 2026-08-30
updated: 2026-08-31
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, db, backend, frontend, docs]
security_touching: false
manual_steps: [nswag-regen]
sprint: 16
---

## Context
- The booking calculator offers `1+kk ... 4+kk, Dum`, which is Czech-specific naming. The owner asked that expansion to other countries be configurable rather than hardcoded.
- The domain is already country-neutral: `Order` stores `Rooms` and `Bathrooms` as ints and `OrderPricingCalculator` computes `BasePrice + PerRoomPrice x (rooms + bathrooms)`. Nothing persists `3+kk`.
- `CountryConfiguration` already holds per-country presentation config (`TaxIdLabel`, `RegistrationNumberFormat`, `LegalRequirementsJson`), so it is the natural home.

Design language and the pre-submission review: [`../../knowledge/design-language.md`](../../knowledge/design-language.md).

## Acceptance criteria
- [x] **AC1** - An ADR records the decision and why the domain does not change.
- [x] **AC2** - A `PropertySizePreset` catalogue exists keyed on country, carrying `Code`, `SortOrder`, `Rooms`, `Bathrooms`, an owned per-language `Translation` dictionary and `IsActive`.
- [ ] **AC3** - CZ and SK are seeded with their existing options; labels come from the catalogue, not a hardcoded list.
- [x] **AC4** - Orders continue to persist ints, so historic pricing stays reproducible when a preset is retired.
- [x] **AC5** - Retiring a preset never breaks an existing order.

## Out of scope
- Launching any new country.
- Changing `OrderPricingCalculator`.

## Implementation notes
Follow `agents/knowledge/patterns-frontend.md` and run section 7 of the design language before opening a PR.
Headings are `Sky700 #0369A1`; primary action `Sky600 #0284C7`; radius from `6 / 12 / 16 / 24 / 32`;
no coloured shadows. Any new shared token lands on Android and iOS in the same PR.

## Status log
- 2026-08-30 - ready (filed from the approved home-page concept)
- 2026-08-31 - AC1, AC2, AC4 and AC5 done; AC3 half done and blocked on the owner.

  **AC1** - `docs/decisions/adr-0056.md` records why the domain does not change: `Order.Rooms` and
  `Order.Bathrooms` are plain integers and `OrderPricingCalculator` computes
  `BasePrice + PerRoomPrice * (rooms + bathrooms)`, so nothing anywhere persists "3+kk". Expansion
  needed a label catalogue and nothing else.

  **AC2** - `PropertySizePreset` carries `CountryId`, `Code`, `SortOrder`, `Rooms`, `Bathrooms`, an
  owned per-language `Translation` dictionary and `IsActive`. Deliberately NOT `ITenantEntity` - it is
  catalogue data like `Country` and `ServiceCity` - so its natural key is `(CountryId, Code)` with no
  `TenantId` term, which also keeps it clear of the single-tenant NULL trap. The migration is in
  (owner regenerated `Initial` as `20260830221715`) and the integration suite builds a real Postgres
  from it.

  **AC3 - half.** CZ and SK are seeded by
  `sql-scripts/seed/insert_property_size_presets.sql`, joined on `IsoCode` rather than a hard-coded
  key, and `GET /api/Country/GetPropertySizes` serves them anonymously with the label already
  resolved for the requested language. What is NOT done is the frontend reading it: the generated
  client has no method for the route and NSwag regeneration is owner-run. That is MS-14, and the
  provider file names the exact one-line swap.

  **AC4 and AC5** hold by construction rather than by change - an order stores the integers, not a
  preset id, so deactivating or relabelling a preset can never make a historic order unpriceable.
  That property is what makes the catalogue safe to edit at all, and it is what ADR-0056 turns on.

  Label resolution falls back requested language -> English -> the CODE. The code is deliberate: a
  blank chip is unpickable and tells nobody anything is wrong, while "CZ_3KK" is still choosable and
  obviously a gap. Eight tests cover it, including the empty-string-is-not-a-label case.

## Review
<!-- reviewer / security / optimizer write verdicts here -->
