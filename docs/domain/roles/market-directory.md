# MarketDirectory — `GetMarkets` + `MarketListItem` (ADR-0058, accepted 2026-09-13; amended the same day for the default-market flag)

**Responsibility (one sentence):** List the markets a customer may browse in — each serviced country
joined to its configured, **active** currency and the two per-market copy figures — and name the
default one, from an anonymous read that never throws on a configuration state.

**Kind:** a MediatR `IRequest` handler (`Features/Markets/GetMarkets.cs`) behind
`GET api/Market/GetOverview` on the Customer and Mobile.Customer hosts, `[AllowAnonymous]`. No
validator, no `BusinessResult`: there is nothing a caller can get wrong.

## Collaborators

- `ICountryRepository.GetServicedAsync` — the candidate set (`IsServiced && IsActive`, **and** — since
  [ADR-0064](/decisions/adr-0064) D1 — a configuration naming an operating company whose `Tenant.IsActive`
  is true; a deactivated company's countries are not candidates).
- `ICountryConfigurationRepository.GetByCountryIdAsync` — the country's `DefaultCurrencyCode` and its
  `InsuranceCoverageAmount` (ADR-0060 D2); `GetDefaultMarketAsync` — the one configuration flagged
  `IsDefaultMarket` (owner ruling 2026-09-13, ADR-0058 amendment).
- `ICurrencyRepository.GetByCodeAsync` / `GetDefaultAsync` — the currency row (`IsActive`, `IsDefault`,
  `NoShowCredit`, ADR-0060 D1). `GetDefaultAsync` throws for its pricing callers; here the throw is
  caught and logged as one more configuration state.
- `ILogger` — the only side effect: a **warning** per serviced country omitted (no configuration,
  unknown or inactive currency); an **error** when no configuration is flagged as the default market
  or the flagged country is not listed (then the currency rule decides), when several markets share
  the default currency (naming every candidate and the one chosen) or when none does.
- Readers: every pre-address customer surface on three clients (ADR-0058 D5); the market selector and
  chip; the copy that interpolates `noShowCredit` / `insuranceCoverageAmount`.

## Contract

- **Listed iff** the country is serviced and active, is served by an operating company that is not
  deactivated, has a configuration whose `DefaultCurrencyCode` names a `Currency`, and that currency
  `IsActive`. A serviced country failing any of the currency predicates is **omitted and warned about**
  — the read is the backstop for seed-authored states the `SetCountryServiced` gate
  (`country.market_not_ready`) never saw; the null-operator log the handler still carries is a belt,
  unreachable now that the repository predicate excludes such a country.
- **`isDefault`:** the listed market whose configuration carries **`IsDefaultMarket`** (at most one,
  by the database; `SetDefaultMarket` moves it; CZE seeded). When nothing is flagged, or the flagged
  country is not listed — logged as an error — the fallback rule decides: among the listed markets on
  the platform default currency, exactly one when there is one; the **lowest `isoCode`** (ordinal)
  plus an error log when several; **none** plus an error log when zero. A pre-selection, never a
  pricing invariant: clients fall to the first listed market and always send the `countryId` they
  resolved.
- **Never throws** on a configuration state. This is the read behind `/`; a 500 here is a 500 for
  every visitor.
- **`isoCode`** (alpha-3) is what a client persists; **`isoAlpha2`** is what the chip prints
  ("CZ · CZK"); **`countryId`** is what a client sends. Ids re-mint on a reseed; codes do not.
- **Order:** as `GetServicedAsync` returns them. Nothing sorts by name.

## Does NOT know

- **The customer.** No session, no tenant, no preference. The chosen market lives on the device
  (a cookie on the web, the settings store on mobile) and never reaches the server except as
  `countryId` on other calls.
- **The address, or any order.** The market of an order is its address's country, resolved elsewhere
  (`ICurrencyResolutionService`); this directory is for the surfaces that have no address yet.
- **Any price.** It says which currency a market is in, never what anything costs. Plus prices are
  `MembershipPlanPrice` rows read by `GetMembershipPlans?countryId=`; catalogue prices are the
  overviews'.
- **Whether a market is *ready*.** Readiness is judged when a country is switched on
  (`SetCountryServiced`, gate 2) and when a currency is activated (`ActivateCurrency`, gate 1); the
  directory only reports what those gates let through and omits what slipped past them.
- **How to format money.** `noShowCredit` and `insuranceCoverageAmount` are numbers in the market's
  currency; the client formats them with its one shared formatter in the device's locale.

**Smell guard:** if a scenario requires this role to read a JWT, a header, `CompanyInfo`, an order, a
price row, or to throw on a configuration state, the responsibility is being stretched — go back to
ADR-0058 D1–D2. The whole contract is "serviced country × active configured currency, one flagged
default, never a 500 on the landing page".
