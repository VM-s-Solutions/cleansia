---
id: T-0721
title: The five 2026-09-13 market rulings — default-market flag, seeded figures, push amount, Stripe Customer per currency
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0710, T-0711, T-0720]
blocks: []
stories: []
adrs: [ADR-0058, ADR-0059, ADR-0060]
layers: [backend, db]
security_touching: true
manual_steps: []
sprint: 16
---

## Context

**The customer market programme (T-0710–T-0720) shipped with five questions parked for the owner in
`questions/open.md` — Q-MARKET-01 to 05.** The owner answered all five on 2026-09-13. Each answer
reversed or completed a position the ADRs had taken as a stated assumption, so every one lands as a
backend change rather than a note. The rulings, recorded here because `open.md` is a queue and not a
record:

- **Q-MARKET-01 — the default market when several share the default currency.** "let's build
  IsDefaultMarket rn". An explicit flag, not the ISO-code tiebreak.
- **Q-MARKET-02 — the insurance ceiling figure.** All cleaners are insured for the amount; they buy
  the insurance themselves. Per market — CZE states 1 000 000 CZK; the EUR figure for SK is to be
  authored when that market opens.
- **Q-MARKET-03 — the no-show credit off-CZK.** "let's make it dynamic rn". A per-currency figure,
  authored on the admin currency form; DEV placeholders seeded so no seeded currency holds a null by
  accident.
- **Q-MARKET-04 — the push announces the credit without the figure.** "let's make it show,
  especially for different currencies in different regions". The lock-screen line carries the amount
  with its own currency.
- **Q-MARKET-05 — Stripe's one-currency-per-Customer rule and the support path.** "there is a
  must-have having this mechanism of resubscribing available". A Stripe Customer per currency, not a
  manual support path.

## Doing

- `CountryConfiguration.IsDefaultMarket` + `SetAsDefaultMarket`, held to at most one row by the
  partial unique index `IX_CountryConfigurations_IsDefaultMarket_Unique` (the
  `IX_Currencies_IsDefault_Unique` shape). `SetDefaultMarket` on
  `PUT api/AdminCountry/{countryId}/default-market` (`Policy.CanUpdateCountry`, the `auth` window)
  with the `SetDefaultCurrency` transaction shape — clear, flush, promote, flush, both audited — and
  the servicing gate plus serviced itself as its validator; `country.default_market_changed_concurrently`
  for the loser of a race. `CountryDetailDto` and `CountryListItem` carry the flag; `GetMarkets`
  pre-selects the flagged market when it is listed and falls back to the default-currency rule with
  an error log otherwise, never a throw. The seed flags CZE.
- Seed: CZE states the 1 000 000 CZK insurance ceiling; EUR 10, PLN 40, GBP 9 and USD 10 are
  placeholder apology credits the owner replaces on the admin currency form before activation.
- `order.no_cleaner_refunded` carries `amount` with its currency: `CancelUnfilledOrders` formats it
  from the credit's own currency row (invariant number, no trailing zeros, a space, the symbol, the
  code when there is none); the APNs display map lists `amount` after `orderNumber` for that key; the
  lock-screen allow-list is `{orderNumber, count, amount}` (ADR-0025 D3 widened by one slot).
- `UserStripeCustomers`: one row per (user, currency), unique Stripe id, Restrict FKs. Both
  subscribe commands resolve the Customer through `IStripeCustomerResolver`: an existing row, else
  the legacy `User.StripeCustomerId` when it has never billed another currency and no row claims
  it, else a new Customer. The legacy field stays and is still written on a user's first Customer.
  A tenant-blind lookup finds a user by any of their Customer ids; `DeleteCurrency` answers
  `currency.in_use` for the table; GDPR erasure deletes the rows with the legacy field.
- `Initial` regenerated once (`20260913080510`).

## NOT

No client work — NSwag and the mobile spec are not regenerated here; the Android and iOS push
bodies that take the `amount` loc-arg are 1efea46e and 611729cf. No local pre-check for the Stripe
currency lock (`membership.stripe_customer_currency_locked` stays as the classification). The DEV
drop the regen owes is run at deploy, not on the branch. The three ADRs still describe the flag as
deferred and the questions as open; that is the docs lane's.

## Acceptance criteria

- At most one configuration carries `IsDefaultMarket`, by the database; promoting a second moves
  the flag rather than failing, and promoting the current default is a no-op.
- `Market/GetOverview` pre-selects the flagged market when it is listed; nothing flagged falls back
  to the default-currency rule with an error log.
- Every seeded currency carries an apology credit; CZE carries the insurance figure and the flag.
- The no-cleaner push names the credit in its own currency, formatted on the server.
- A customer whose CZK Plus was cancelled subscribes in EUR on a second Stripe Customer; CZK adopts
  the legacy one; erasure and currency deletion know the table.

## Review

- Tests: `SetDefaultMarketHandlerTests`, `SetDefaultMarketValidatorTests`, `GetMarketsHandlerTests`,
  `MarketDirectoryRouteTests` (Admin + Customer hosts), `SeededCataloguePricingTests` (real seed),
  `CountryConfigurationDefaultMarketIndexTests` (the index over real Postgres),
  `CancelUnfilledOrdersTests` + `CancelUnfilledOrdersApologyCurrencyTests`, `FcmMessageFactoryTests`,
  `StripeCustomerResolverTests` + wiring pin, `MembershipSubscribeUsesPerCurrencyCustomerTests`,
  `UserStripeCustomerTests` (real Postgres), `MembershipResubscribeAcrossCurrenciesRouteTests`,
  `SubjectResidueErasureTests`; `RateLimitCoverageGuardTests` and
  `CatalogLifecycleEndpointPermissionTests` pin the new endpoint's window and permission.
- Sabotage: each new rule broken once, its named test red, file restored byte-exact, green.
- Catalog-edit routing: no `patterns-backend.md` / `consistency.md` entry added — every shape mirrors
  an existing one (`SetDefaultCurrency` for the flag move, `IX_Currencies_IsDefault_Unique` for the
  index, `MembershipPlanPrice` for the per-currency row, `UpdateCountryMarketContent` for the PUT).
- Review findings applied after e3fb1227: the index proof over real Postgres, the two reflection pins
  on the new endpoint, this ticket, and the source comments citing `Q-MARKET-0x` reworded to the
  repo's `owner ruling 2026-09-13` form.
