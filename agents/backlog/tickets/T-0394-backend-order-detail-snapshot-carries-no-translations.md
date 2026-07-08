---
id: T-0394
title: "Backend — order-DETAIL service/package snapshot carries no translations, so order-detail names render frozen-English in every non-English app"
status: proposed
size: M
owner: backend
created: 2026-07-08
updated: 2026-07-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, db, ios, android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-5 C-orders-i18n review (minor, CASE b disclosed by the implementer)
---

> **Found by fix-round 5 (owner remark #5 — catalog names in the app language).** The order **list** card is now
> localized client-side (its `OrderListItem.selectedServices/selectedPackages` DTOs carry `translations`, so
> `servicesSummary` picks `translations[appLang].name ?? name`). But the order **detail** DTOs — `ServiceDetails`
> and `PackageDetails` — carry **no `translations` field**, so `OrderServicesCard`/`OrderPackagesCard` render
> `service.name ?? "—"` frozen at the English name captured when the order was placed, regardless of the app
> language. The iOS implementer correctly did NOT fabricate a translation; this is the residual server-side half.

## Context
- iOS: `CleansiaCustomerApi/Models/ServiceDetails` + `PackageDetails` have `name`/`description` but no
  `translations` (verified in fix-round 5). Consumed by `Orders/OrderDetailDetailsCards.swift`.
- The live-catalog DTOs (`ServiceListItem`, `CategoryDto`, `PackageServiceSummary`) DO carry `translations`
  (`ServiceMappers.cs` maps `service.Translations.ToDictionary()`), which is why booking/home/order-list localize.
- Root cause: the order line-item **snapshot** persists the display name at booking time (English) and the
  order-detail projection surfaces only that snapshot — no `translations`, and no `serviceId`/`packageId` a
  client could use to re-localize from the live catalog.

## Acceptance criteria
- [ ] **AC1 (localizable detail)** — Given a placed order and an app in cs/sk/uk/ru, When the order-detail
  service/package cards render, Then the names show in the app language. Choose ONE and record:
  - (a) add `translations` (the per-language name/description dict) to the order-detail snapshot / the
    `ServiceDetails`/`PackageDetails` DTOs, then wire iOS + Android to pick `translations[appLang]`, OR
  - (b) include the live `serviceId`/`packageId` on the detail DTO and let the client re-localize from the
    already-cached catalog, OR
  - (c) localize server-side on the order-detail projection using the request language (Accept-Language) — note
    there is currently NO request-localization middleware in the mobile hosts, so (c) implies adding it.
- [ ] **AC2 (contract)** — DTO/spec change → re-dump the mobile spec + regenerate BOTH mobile clients
  (MANUAL_STEP); the order-list and order-detail localization then share one shape.
- [ ] **AC3 (parity + non-regression)** — iOS and Android order-detail localize identically; backend tests green;
  historical orders (whose snapshot predates the change) degrade gracefully to the stored English name.

## Out of scope
- The order-**list** localization (already done client-side in fix-round 5).
- General request-localization middleware unless option (c) is chosen.

## Status log
- 2026-07-08 — filed `proposed` by pm from the fix-round-5 C-orders-i18n review. The client did the localizable
  half (list); this is the server-snapshot half that no client can fix. Medium priority: order-detail is a
  secondary surface, and the English name is at least correct, just not translated.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
