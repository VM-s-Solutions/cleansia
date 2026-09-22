---
id: T-0711
title: The market directory and the per-market copy figures
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: []
blocks: [T-0713, T-0714, T-0716, T-0718, T-0720]
stories: []
adrs: [ADR-0058, ADR-0060]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

**Every pre-address customer surface renders the platform default, and the money figures in customer
copy are hand-typed CZK literals.** The owner ruled (2026-09-12) that a customer's market is chosen and
remembered, defaulting to the platform default market, and that copy figures depend on the market, not
the translation. ADR-0058 D1–D2 define the market directory; ADR-0060 D1–D2 move the two figures onto
data: the no-show apology onto the currency, the insurance ceiling onto the country configuration.

## Doing

- `Currency.NoShowCredit` + `SetNoShowCredit`; `CountryConfiguration.InsuranceCoverageAmount` +
  `UpdateMarketContent`; `Country.IsoAlpha2` (the two letters the market chip prints; orchestrator
  ruling Q-DESIGN-01) seeded for all 249 ISO 3166-1 rows.
- `BookingPolicy.NoShowCreditCzk` deleted; `CancelUnfilledOrders` reads the order currency's figure
  and pays into the account in that currency; null pays none and sends the plain cancellation.
- `GET api/Market/GetOverview` on both customer hosts (`GetMarkets`, `MarketListItem`): serviced
  country + configured ACTIVE currency; default = lowest ISO code on the default currency with an
  error log when several, none flagged with an error log when zero; never throws.
- `SetCountryServiced(true)` gate → `country.market_not_ready` (ADR-0058 D7 gate 2).
- Admin: `CreateCurrency`/`UpdateCurrency` + `NoShowCredit`; admin currency DTOs;
  `PUT api/AdminCountry/{id}/market-content` (`UpdateCountryMarketContent`,
  `country.configuration_missing`); `CountryDetailDto` + `InsuranceCoverageAmount`/`HasConfiguration`/
  `IsoAlpha2`; `CountryListItem` + `IsoAlpha2`; `CreateCountry`/`UpdateCountry` accept `IsoAlpha2`
  (`country.iso_alpha2_invalid`).
- Checker: the C# const read becomes `INTERIM_NO_SHOW_CREDIT_CZK = 250` until T-0715/17/19 flip the pins.
- Seeds (CZK 250, EUR null, insurance null, alpha-2 on every country), `Initial` regenerated.

## NOT

No client copy or UI; no `ActivateCurrency` gate on the credit; no `IsDefaultMarket` flag; no admin
writer for the rest of `CountryConfiguration`; no seeded insurance figure; `Country/GetServiced` and
`Currency/GetOverview` untouched; NSwag / mobile-spec regens (orchestrator's, after T-0710 lands).

## Acceptance criteria

See the programme ticket plan (AC1–AC9): market listing, unready country omitted, several / zero
default-currency markets, the servicing gate, the per-currency apology, the credit authoring, the
insurance figure round trip, and the constant gone.

## Review

- Tests: `GetMarketsHandlerTests`, `SetCountryServicedValidatorTests`,
  `UpdateCountryMarketContentValidatorTests`, `CountryIsoAlpha2ValidationTests`,
  `CurrencyLoyaltyDivisorValidationTests` (+ no-show credit), `CancelUnfilledOrdersTests` (three
  currency states), `CancelUnfilledOrdersApologyCurrencyTests` (real Postgres),
  `MarketDirectoryRouteTests` (Customer + Admin hosts end to end), checker self-test.
- Sabotage: each new rule broken once, its named test red, file restored byte-exact (sha256), green.
- Catalog-edit routing: no `patterns-backend.md` / `consistency.md` entry added — every shape here
  mirrors an existing one (`GetServiceOverview` for the IRequest read, `UpdateCountry` for the PUT,
  `LoyaltyPointsDivisor` for the authored per-currency figure, `RefundStripeFixedFee` for the
  per-country number).
- Absorbed: the three request-logging anti-vacuity guards pinned the route census to 400–1000 and the
  tree sat at exactly 1000; the three new routes moved the ceiling to 1500.
