---
id: T-0716
title: Android customer — the market preference, chip and readers
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0710, T-0711, T-0712]
blocks: [T-0717, T-0720]
stories: []
adrs: [ADR-0058, ADR-0059]
layers: [android]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

The Android customer app read the catalogue and the Plus plans with no country and labelled
membership figures with the catalogue's default code; the tree at HEAD did not even compile against
the regenerated client (`monthlyPriceCzk`).

## Doing

- `core/market/`: `MarketDtos.kt` (`MarketListItem`, `MarketState { Unavailable, Resolved }`),
  `MarketApi.kt` (refuses a row missing identity/label fields; copy figures nullable), `MarketModule.kt`
  (`@NoAuthRetrofit`), `MarketRepository.kt` (`@Singleton`; `ensureLoaded` / `refresh` / `select`;
  stored-if-listed → `isDefault` → first → persist; failure keeps the last list; an empty list is
  `Unavailable`). `AppSettings.market` beside `language`.
- `MarketScreen` + `MarketViewModel` (Language twin, `Icons.Outlined.Map`), `Routes.Market`, Profile
  row and home `AddressTopBar` chip only with ≥ 2 markets; `CleansiaChip` gained a `role` parameter
  (`Role.Button` for the chip).
- Readers: home catalogue keyed on the market's `countryId` (address still wins in `BookingViewModel`;
  `reset()` hands the market back); `MembershipApi.getPlans(countryId)` cached per market,
  `subscribePhase1/2(countryId)` with the chosen market's id even from a booking, `idempotencyToken`
  now mapped; `MembershipViewModel` follows `marketRepository.state`; labels from `plan.currencyCode`
  / `GetMine.currencyCode` (`MembershipViewModel.currencyCode` removed); switch-to-annual only when the
  yearly plan's currency equals the membership's; rewards floor only when the market currency is the
  directory default; the recurring form prices the market before an address and gained
  `RecurringCatalogState` (Loading/Error/Loaded) with a retry and a gated submit.
- `SubscribePlusScreen`: labels from the plan, `NotAvailableInMarket` empty state, Google Pay
  `countryCode = market.isoAlpha2 ?: "CZ"` (deviation from the ticket's NOT — reported in ADR-0058).
- Strings ×5: `market_title`, `market_hint`, `profile_row_market`, `market_chip_a11y`,
  `plus_not_available_in_market`, `error_membership_plan_not_priced_in_currency`,
  `error_membership_stripe_customer_currency_locked` (no currency clause).

## NOT

No copy figures (T-0717); the booking/order Google Pay sheets untouched; no partner app; no server
sync; no anonymous mode.

## Acceptance criteria

AC1–AC10 of the programme plan (default on first run, remembered, delisted, unavailable, chip opens
the picker, address overrides, Plus follows the market, membership keeps its currency, wire rosters,
error keys).

## Review

- Commit `6c25b2e0` (46 files). `:core:testDebugUnitTest` 235/235, `:customer-app:testDebugUnitTest`
  1033/1033 (+69); `compileDebugKotlin` clean. 13/13 sabotages caught. New tests: `MarketRepositoryTest`
  (17), `MarketWireTest` (7), `MarketViewModelTest` (2); rosters in `MembershipWireTest`,
  `ServiceAreaWireTest` (+`isoAlpha2`), `BackendKeyStringsTest`.
- Absorbed: a stale "BLOCKED ON nswag-regen" comment in `MembershipApi` was wrong (the generated
  command has carried `idempotencyToken`).
- Findings: `MarketRepository` needed an E9 allowlist entry in `consistency.md` (added later by the
  orchestrator, `52fb753c`); the checker's `amountsIn` reads the `1` in `%1$s`.
