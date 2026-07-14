---
id: T-0402
title: "Backend/mobile — partner order-detail DTO carries no translations (service/package names frozen-English), the partner analogue of T-0394"
status: proposed
size: M
owner: backend
created: 2026-07-13
updated: 2026-07-13
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
- [ ] **AC1** — add `translations` to the partner order-detail service/package DTOs (mirror the customer
  `ServiceDetails`/`PackageDetails` + the `MapToDetails` mapper population from `Service.Translations` /
  `Package.Translations` — exact T-0394 shape), so the field is populated on the partner order-detail response.
- [ ] **AC2** — re-dump the partner mobile spec + regenerate the partner API client (MANUAL_STEP); wire the
  partner `OrderDetailCards.ScopeCard` names via the app-language `translations[lang].name ?? name`, matching
  the partner order-list + the customer app.
- [ ] **AC3** — backend `dotnet test` green (mapper emits the dict); iOS partner builds; historical orders
  degrade to the snapshot English name.

## Out of scope
- The customer order-detail (done — T-0394).
- Android partner (has the same leak — separate parity ticket).

## Status log
- 2026-07-13 — filed `proposed` from the partner localization round. Client did the localizable half (status,
  payment, dates, tabs); this is the server-snapshot half no client can fix.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
