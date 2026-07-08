---
id: T-0395
title: "Android — catalog names not localized on Home 'Popular packages' + order-list summary (iOS localized them in fix-round 5; Android now lags)"
status: proposed
size: S
owner: android
created: 2026-07-08
updated: 2026-07-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix2 fix-round-5 (A-shell+home + C-orders-i18n reviews) — iOS localized these; Android parity follow-up
---

> **Found by the iOS fix-round-5 port (owner remark #5).** iOS now localizes catalog names on Home "Popular
> packages" and the order-list summary using the seeded `translations` dict + the app language. Android has the
> `localizedName(translations, name)` helper (used in booking `ServicesStep.kt`) but does NOT apply it on these
> two surfaces, so Android still shows English there. This is the reverse of the usual direction — iOS is now
> ahead; Android should catch up so the platforms match.

## Context
- Android Home popular-packages card: `HomeTab.kt:915` renders `text = pkg.name.orEmpty()` (raw English).
- Android order-list summary: `OrdersTab.kt:521-523` uses `it.name` (raw English) for the services/packages
  summary line.
- The helper already exists: `booking/ServicesStep.kt:111-118` `localizedName(translations, name, lang)` reads
  the active app locale (`LocalConfiguration.current.locales[0]`).

## Acceptance criteria
- [ ] **AC1 (Home)** — Home "Popular packages" card titles render `localizedName(pkg.translations, pkg.name)` in
  the active app language, matching iOS.
- [ ] **AC2 (order-list)** — the customer order-list summary localizes service/package names via the same helper,
  matching iOS `servicesSummary`.
- [ ] **AC3 (non-regression)** — `:customer-app` compiles; home/orders tests green; order-detail names remain a
  separate concern (backend snapshot — see T-0394); recent-booking titles that use order-snapshot names (no
  translations) stay raw on BOTH platforms.

## Out of scope
- Order-detail name localization (backend snapshot — T-0394).
- iOS (already done in fix-round 5).

## Status log
- 2026-07-08 — filed `proposed` by pm from the fix-round-5 reviews. Small: the helper exists; it's two call-site
  swaps. Medium priority (visible catalog English in non-English Android builds).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
