---
id: T-0718
title: iOS customer — the market preference, chip and readers
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0710, T-0711, T-0712]
blocks: [T-0719, T-0720]
stories: []
adrs: [ADR-0058, ADR-0059]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

The T-0716 mirror on iOS: the customer app read the catalogue and the plans with no country and
guessed the membership's unit from the catalogue default.

## Doing

- Core: `MarketPreferenceStore` protocol (`marketIsoCode` / `setMarket` / `clearMarket`, key
  `settings.market`) conformed to by `UserDefaultsAppSettingsStore` — a separate protocol so the partner
  app's five `FakeSettings` conformers keep compiling; `/api/market/getoverview` on the customer
  `AnonymousAllowList`; `error.membership.plan.not_priced_in_currency` and
  `error.membership.stripe_customer_currency_locked` ×5.
- Customer: `MarketClient` (`Market.chipLabel` = "CZ · CZK" from `isoAlpha2`), `MarketStore`
  (`loading / unavailable / resolved`, single-flight refresh, 30 s staleness — the unavailable state is
  always stale, an empty list is unavailable, failure keeps the last list); `MarketPickerView` over the
  unchanged `PreferencePickerList`; Preferences row and home `MarketChip` (`.isButton`, 3 pt hit inset)
  only with ≥ 2 markets; the shell prefetch reads the directory first.
- Readers: home catalogue after the directory; `MembershipManagementClient.getPlans(countryId:)` /
  `subscribe(countryId:)` with the chosen market's id, re-read on market change once ever requested;
  `MembershipPlan.currencyCode` required, `MyMembership.price / monthlyEquivalentPrice / currencyCode`
  optional; `MembershipViewModel.currencyCode` removed; `annualSwitchPlan` only when the listed yearly
  plan is in the membership's currency; the currency-lock copy substitutes `membership_currency_locked_in`
  with `GetMine.currencyCode` when known; "not available" state, a `membership_plans_load_failed` retry
  state, and the F-1 zero-price fix; booking `catalogCountryId = address ?? market`; rewards
  `TierFloorLabel`; recurring `canAdvance(step:)` + edit-mode prune against the template's market.

## NOT

No copy figures (T-0719); PaymentSheet configuration untouched; partner app untouched; no anonymous
mode. No Swift toolchain in the lane — the owner's Xcode build confirms the generated names
(`CustomerMarketAPI.marketGetOverview`, `membershipGetPlans(countryId:)`, the renamed response fields).

## Acceptance criteria

AC1–AC10 of the programme plan, read with `AppSettingsStore` / `MarketPickerView` / `CatalogClient` /
`MembershipManagementClient` / `OrdersFormat.price`.

## Review

- Commit `144a3b07` (44 files). `check-ios-symbols` clean (8 → 0 problems); the lane's catalogue
  checker green (roster 103 keys). Tests written, to run on the Mac: `MarketStoreTests` (17),
  `MembershipViewModelTests` (+12), `CustomerWireContractTests` (+4), `HomeTabViewModelTests` (+5),
  `RewardsViewModelTests`, `LoyaltyPresentationTests`, `BookingViewModelTests` (+5),
  `CreateRecurringViewModelTests` (+4), Core `AppSettingsStoreTests` / `AnonymousAllowListTests` /
  `CustomerErrorVoiceTests`. 10 sabotages against the static checks caught; the Xcode sabotage list is
  in the lane report.
- Findings: Android's annual-switch dialog had the same currency-mismatch shape (closed in T-0716);
  `PushLocKeyCatalogTests.events` does not list `order.no_cleaner_refunded`; design F-4/F-5 stand.
