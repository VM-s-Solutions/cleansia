# MembershipPlanPrice + `IMembershipPlanPriceRepository` (ADR-0059, accepted 2026-09-13)

**Responsibility (one sentence):** Name what **one billing period** of a membership plan costs in
**one currency** and which **Stripe Price** charges it — so that the figure a customer is shown and the
figure Stripe bills are the same row.

**Kind:** entity (`Cleansia.Core.Domain/Memberships/MembershipPlanPrice.cs`, `Auditable`, platform
config — no `ITenantEntity`, pinned by `MembershipPlanPlatformConfigStructuralTests`) + repository
(`GetForPlansAsync(planIds, currencyId)`, `GetForPlanAsync(planId, currencyId)`,
`GetAllForPlanAsync(planId)`, `IsStripePriceIdUsedAsync`). The `PackagePrice` archetype plus one
column.

## Collaborators

- `MembershipPlan` (`MembershipPlanId`, FK Cascade — a price has no meaning without the plan; no
  collection navigation on the plan, exactly as `PackagePrice`). The plan supplies the cadence
  (`MonthlyEquivalentOf(price)`); the row supplies only the number.
- `Currency` (`CurrencyId`, FK Restrict — `CurrencyRepository.IsInUseAsync` names this table so
  `DeleteCurrency` answers `currency.in_use` rather than a raw 23503).
- The two unique indexes: `IX_MembershipPlanPrices_MembershipPlanId_CurrencyId` (one price per plan per
  currency, **no tenant term**) and `IX_MembershipPlanPrices_StripePriceId` (a Stripe Price is
  single-currency and single-product; two rows naming one is an admin typo — the validator refuses it
  first as `membership.plan.stripe_price_already_used`, the index is the backstop).
- Readers that decide money: `GetMembershipPlans` (the market's rows, only plans that have one),
  `CreateMembershipCheckoutSession` / `CreateMembershipSubscription` (the market's row → Stripe),
  `SwapMembershipPlan` (the **membership's** currency's row → Stripe), `GetMyMembership` (the
  membership's currency's row → the label).
- Writer: `MembershipPlanPricing.UpsertAsync`, shared by `CreateMembershipPlan` and
  `UpdateMembershipPlan` — one row per currency code the admin sent, created or updated; codes not
  sent keep their rows; rows are never deleted (deactivate the plan instead).

## Contract

- **`Price`** is the charge for one billing period — the full annual charge on a yearly plan, not a
  per-month figure. `MonthlyEquivalent` and `SavingsPercentVsMonthly` are computed per currency in the
  query, never stored.
- **Absent row = not on sale in that currency.** `GetPlans?countryId=` omits the plan; both subscribe
  paths and the swap refuse `membership.plan.not_priced_in_currency` before any Stripe object exists.
  Absence is a legal state — the admin form does not require every active currency (unlike the
  catalogue's `MustCoverAllActiveCurrencies`), and `ActivateCurrency` does not check plans.
- **`Price >= 0`**, `StripePriceId` non-empty and ≤ 64, and not already charging another row (another
  plan's, or this plan's row in another currency) — `MembershipPlanPriceEntryValidator`, with the
  plan's own row in the same currency excepted on update.
- **The admin list shows the platform-default-currency row and `null` when absent** (rendered "—"),
  not the package list's 0: in the default currency absence is a real state and "0 CZK" reads as free.

## Does NOT know

- **The customer, or the market.** Which currency to look up is the caller's: the chosen market's
  (subscribe, list) or the membership's own (swap, my membership). This row is one (plan, currency)
  fact.
- **The subscription.** `UserMembership.CurrencyId` records what a subscription was created in and
  never changes; this row can be edited or re-pointed at a new Stripe Price afterwards without touching
  any subscription — Stripe holds the live price.
- **Any other currency's price.** There is no conversion, no "derive EUR from CZK", no default-currency
  fallback. A plan priced only in CZK is unpriced in EUR.
- **Stripe's object.** The id is entered by an admin; nothing here calls Stripe to create, verify or
  read a Price. A wrong id fails at subscribe time as a gateway error.
- **Benefits.** Discount, cancellation window, waiver quota and trial are the plan's, currency-free,
  and apply to an order in any currency (ADR-0059 D2).

**Smell guard:** if a scenario requires this row to carry two intervals, a per-month and a per-year
figure, a tenant, a customer, or a fallback to another currency's price, the responsibility is being
stretched — go back to ADR-0059 D1. The whole contract is "one plan, one currency, one price, one
Stripe Price".
