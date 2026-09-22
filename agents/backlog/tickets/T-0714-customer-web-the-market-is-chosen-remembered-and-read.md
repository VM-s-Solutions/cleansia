---
id: T-0714
title: Customer web — the market is chosen, remembered, and read
status: done
size: L
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0710, T-0711, T-0712]
blocks: [T-0715, T-0720]
stories: []
adrs: [ADR-0058, ADR-0059]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Every pre-address surface of the customer web app rendered the platform default and the membership
surfaces labelled with the default currency code. ADR-0058 gives the customer a chosen market held in
one cookie so the SSR render and the browser agree; ADR-0059 makes the Plus surfaces read it.

## Doing

- `market-preference.ts`: `readPreferredMarket` (cookie header → code), `resolveMarket`
  (stored-if-listed → `isDefault` → first), `persistPreferredMarket` (**cookie only**, no localStorage).
- `customer-stores/market`: `markets[]`, `selectedIsoCode`, `loadFailed`; selectors `selectMarket`,
  `selectMarketCountryId` (null when unresolved), `selectMarketCurrencyCode`, `selectMarketNoShowCredit`,
  `selectMarketInsuranceCoverageAmount`, `selectHasMarketChoice`; `initializeMarket` APP_INITIALIZER on
  both branches, never throws, persists in the browser only; retry on `NavigationEnd` while failed.
- `<cleansia-market-switcher variant="pill" | "chip">` in navbar (desktop + mobile-menu row `nav.market`)
  and footer (≥ 2 markets), the quick-quote chip (`{isoAlpha2} · {currencyCode}`; control / static /
  absent); pill SCSS lifted into one shared mixin with the language switcher.
- Readers: home strips, `/services`, quick quote (`countryId`, presets via
  `Country/GetPropertySizes?isoCode=` — the hardcoded CZ set deleted), wizard step 0 (address still
  wins), Plus page / home teaser / wizard Plus step (`GetPlans?countryId` = the **market's**), checkout
  `countryId`, rewards floor line (default-currency market only), membership management
  (`currencyCode` from `GetMine`, `switchablePlans` in the membership's currency).
- Empty states per the design spec; the home teaser is omitted with no plan (F-2); the wizard skips
  the Plus step when unavailable; `pages.plus.not_available_in_market` ×5;
  `api.membership.plan.not_priced_in_currency` and `api.membership.stripe_customer_currency_locked` ×5
  (no `{{currency}}` clause — the interceptor renders `api.*` without params); dead keys
  `pages.home.plus.lead_plain`, `pages.home.quote.size_*` deleted.

## NOT

No copy figures (T-0715); no partner/admin; no server sync; no localStorage mirror; no Accept-Language
inference; the language switcher's own cookie/localStorage pair untouched (reported).

## Acceptance criteria

AC1–AC12 of the programme plan; AC1's "transfer-cache hits only" is unattainable for any lane today
(see Review) and was pinned at the store level instead.

## Review

- Commit `76791b8a`. Jest per lib all green (services 88, customer-services 63, customer-stores 56,
  components 10, home 60, plus 19, profile 76, order-wizard 266, rewards 23, services-catalog 32,
  legal-pages 14, app incl. parity 47); lint 0 errors; typecheck 3/3; Playwright smoke 2/2 (SSR ran
  with the market read refused — the no-market render completed). 18 sabotages caught.
- Findings (not fixed): **the customer app's HTTP transfer cache is dead** — `transferCacheInterceptorFn`
  skips any request sent `withCredentials` (Angular ≥ 20.3.27) and `CustomerAuthInterceptorFn` marks
  every own-API request that way, so every SSR page refetches on hydrate; `PlusPageFacade.startCheckout`
  shows a second toast on a business refusal; `plus-page.component.ts` unused import.
