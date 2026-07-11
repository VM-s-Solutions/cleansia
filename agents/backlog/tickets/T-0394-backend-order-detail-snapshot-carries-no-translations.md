---
id: T-0394
title: "Backend — order-DETAIL service/package snapshot carries no translations, so order-detail names render frozen-English in every non-English app"
status: in-review
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
manual_steps:
  - nswag-regen (web partner/admin/customer TS clients — ServiceDetails/PackageDetails now carry `translations`, surfaced via GetOrderDetails on every web host)
  - android-client-regen + Android OrderDetail wiring parity (the shared spec customer-mobile-api.json was re-dumped in-branch; Android must regen its Kotlin client and localize OrderServicesCard/OrderPackagesCard the same way iOS now does — AC3 parity)
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
- 2026-07-08 — backend implemented option **(a)** + iOS vertical done (`in-review`). Details:
  - **Backend (done):** added `Translations` (`Dictionary<string, Translation>`, last positional — same type +
    `.ToDictionary()` idiom as `ServiceListItem`/`PackageListItem`) to `ServiceDetails` + `PackageDetails`;
    populated in `ServiceMappers.MapToDetails` / `PackageMappers.MapToDetails` from
    `service.Translations.ToDictionary()` / `package.Translations.ToDictionary()`. The order-detail path
    (`OrderMappers.MapToDetail` → `MapToDetails`) already loads `Service`/`Package` via
    `OrderRepository.GetByIdAsync`, and `Translations` is a serialized JSON `text` column that materializes
    eagerly with the entity — **no Include change needed** (verified). New unit test
    `OrderDetailMapperTranslationsTests` (3 cases: service dict, package dict, untranslated→empty-dict +
    English-name fallback). `dotnet test Cleansia.Tests` = **1779 passed, 0 failed**; `dotnet build` green.
  - **Spec re-dump (done, in-branch):** `src/cleansia_android/openapi/customer-mobile-api.json` re-dumped
    against a disposable Postgres (docker/colima `postgres:16`, `dbContext.Migrate()` + seed on Development
    boot, fetched `http://localhost:5004/swagger/v1/swagger.json`) — same disposable-postgres approach as
    T-0370 (`5252bfb9`). Semantic diff = **exactly** `+translations` on `ServiceDetails` + `PackageDetails`
    (`{type:object, additionalProperties:$ref Translation, nullable}` — identical to `ServiceListItem`), no
    path/other-schema drift. Spec NOT hand-edited.
  - **iOS (done):** regenerated `CleansiaCustomerApi` (gitignored) via `generate-api-clients.sh customer` —
    both models now carry `translations: [String: Translation]?`. Wired `OrderServicesCard`/`OrderPackagesCard`
    (`OrderDetailDetailsCards.swift`) to `@Environment(\.locale)` + new `OrdersFormat.localizedCatalogName` /
    `localizedCatalogDescription` helpers (reuse `OrdersFormat`'s existing `localizedName`/`nonBlank` idiom —
    same resolution as the order-list `servicesSummary` + Home `recentBookingTitle`), keeping the frozen
    English snapshot as the fallback for pre-change orders. 4 new `OrdersFormatTests`. swiftformat --lint = 0,
    swiftlint --strict = 0, `xcodebuild test` CleansiaCustomer iPhone 17/iOS 26.3 = **485 passed**, build-only
    iPhone 14/iOS 16.4 = **BUILD SUCCEEDED**.
  - **Remaining (owner manual_steps):** (1) web NSwag regen (partner/admin/customer TS clients — same detail
    DTOs surface on the web hosts); (2) Android Kotlin client regen from the re-dumped shared spec + the
    Android OrderDetail card wiring for AC3 parity. The order-detail package `includedServices`/
    `includedServiceItems` sub-line stays English (those DTOs carry no per-item translations — out of scope,
    matches the ticket's name-focus).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
