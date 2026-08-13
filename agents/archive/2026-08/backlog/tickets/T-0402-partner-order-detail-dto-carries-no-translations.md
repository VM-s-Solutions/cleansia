---
id: T-0402
title: "Backend/mobile — partner order-detail DTO carries no translations (service/package names frozen-English), the partner analogue of T-0394"
status: done
size: M
owner: backend
created: 2026-07-13
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, ios, android]
security_touching: false
priority: medium
manual_steps:
  - spec re-dump (partner-mobile) + partner NSwag/openapi client regen after the DTO change
sprint: 12
source: phase/ios-fix3 partner localization round — the customer T-0394 pattern, unported to the partner order-detail DTO
---

> **Found while localizing the partner order detail.** The customer order-detail DTO carries a per-line
> `translations` map (T-0394), so the customer order-detail service/package names localize. The PARTNER
> mobile `ServiceDetails`/`PackageDetails` do NOT carry `translations`, so the partner order-detail
> service/package names (Eco-Friendly Cleaning, etc.) render frozen order-snapshot English regardless of the
> app language. The iOS localization round did NOT fake it (labels localize; names stay as-is).

## Acceptance criteria
- [x] **AC1** — add `translations` to the partner order-detail service/package DTOs (mirror the customer
  `ServiceDetails`/`PackageDetails` + the `MapToDetails` mapper population from `Service.Translations` /
  `Package.Translations` — exact T-0394 shape), so the field is populated on the partner order-detail response.
- [x] **AC2** — re-dump the partner mobile spec + regenerate the partner API client (MANUAL_STEP); wire the
  partner `OrderDetailCards.ScopeCard` names via the app-language `translations[lang].name ?? name`, matching
  the partner order-list + the customer app.
- [x] **AC3** — backend `dotnet test` green (mapper emits the dict); iOS partner builds; historical orders
  degrade to the snapshot English name.

## Out of scope
- The customer order-detail (done — T-0394).
- Android partner (has the same leak — separate parity ticket).

## Status log
- 2026-07-13 — filed `proposed` from the partner localization round. Client did the localizable half (status,
  payment, dates, tabs); this is the server-snapshot half no client can fix.
- 2026-07-19 — **Android half done.** Premise mostly superseded: `ScopeCard` + the `resolveTranslatedName`/
  `localizedScopeName` seam + 7 resolution/fallback tests already landed in `ce067ca5`, and the regenerated
  partner client carries `translations` on `ServiceDetails`/`PackageDetails`. Remaining gap closed:
  `CleaningChecklist` rendered raw `s.name`/`p.name` from the same DTOs — now resolves via
  `localizedScopeName` (app language → `translations[lang].name` → snapshot-English fallback), same as
  ScopeCard. `:partner-app:testDebugUnitTest` green (122 tests / 25 suites). iOS half pending; parent flips
  overall status. iOS parity note: the checklist rows must apply the same resolution rule as the scope card.
- 2026-07-19 — **iOS half done → ticket done.** Same two surfaces closed on the iOS partner: `translations`
  threaded through the `OrderDetail` domain mapping (`OrderDetailService`/`OrderDetailPackage`), and both
  `ScopeCard` + `CleaningChecklistView` resolve via the new `ScopeTranslation.resolveTranslatedName` seam +
  `localizedName(for:)` extensions (app language → `translations[lang].name` → snapshot-English fallback,
  nil/blank-name degradation — the Android seam mirrored). Checklist tick keys keep the RAW snapshot-name
  fallback so persisted ticks survive a language switch. 8 XCTest cases (the 7 Android cases + one
  locale-tag-extraction case); partner suite 463/465 on iPhone 17 + the iPhone14-iOS16 floor (the 2 known
  LocalizableCatalogFormatTests TCC locals only); both schemes BUILD SUCCEEDED; SwiftFormat --lint +
  SwiftLint --strict clean.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → blocked)** — backend half done `bcd375d5` (premise superseded — the wire already emits translations). REMAINING is regen-gated (owner MANUAL_STEP): partner-mobile spec re-dump + 3 partner client regens, then wire ScopeCard/mobile cards.
